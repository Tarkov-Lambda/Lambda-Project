using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main;
using Lambda.Core.Main.Economy;
using Lambda.Shared.Models;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using PacketWarden.RateLimiting;
using MemoryPack;
using System.Diagnostics;
using UnityEngine;
using Comfort.Common;
using Lambda.Core.Main.Gamemode;
using System.Collections.Generic;
using PacketWarden.TimeSync;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct BuyItemPacket : IPacket, IAuthoredPacket, ITrackablePacket, IServerTimestampedPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    [MemoryPackAllowSerialize]
    public ItemPlacement placement;

    [MemoryPackAllowSerialize]
    public Item item;

    public Guid ID { get; set; }
    public double Timestamp { get; set; }
}

// да заебато все работает че доебался то
public class BuyItemPacketWarden : LambdaPacketWarden<BuyItemPacket>
{
    // private readonly KeyedDebouncer<int> _resyncDebouncer = new();

    public Dictionary<Guid, List<BuyItemPacket>> RoundTransactions { get; private set; } = new();

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(0.15);

#if DEBUG
    protected override bool ShouldLog => true;
#endif
    protected override bool ShouldNotifyAboutRejection => true;

    public BuyItemPacketWarden()
    {
        EventBus.OnEnter += OnMatchStateEnter;
    }

    public override void Dispose()
    {
        EventBus.OnEnter -= OnMatchStateEnter;

        // _resyncDebouncer.Dispose();
        base.Dispose();
    }

    public void OnMatchStateEnter(MatchState state)
    {
        if (state is MatchState.Cleanup)
        {
            // RoundTransactions.Clear();
            // _resyncDebouncer.Dispose();
        }
    }

    public void Send(Item item, ItemPlacement placement, Player player)
    {
        var packet = new BuyItemPacket
        {
            Player = player,
            item = item, // this item is only a template, the server clones it before application
            // placement = placement,
        };

        DispatchPacket(ref packet);
    }

    protected override bool ValidatePacket(BuyItemPacket packet, int peerId, out string rejectionReason)
    {
        if (H.Session.matchState != MatchState.Cleanup)
        {
            if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
            {
                var playerScore = packet.Player.Context;

                if (playerScore.Money < itemData.price)
                {
                    rejectionReason = "You don't have enough money";
                    return false;
                }

                if (playerScore.HasReachedLimit(itemData))
                {
                    rejectionReason = $"Limit reached for this round ({itemData.maxBuy})";
                    return false;
                }
            }

            // TODO: better tracking for this
            // if (packet.item is HeadwearItemClass)
            // {
            //     if (packet.Player.CountAvailableArmorPlateSlots() > 0)
            //     {
            //         rejectionReason = $"You must buy armor first";
            //         return false;
            //     }
            // }
        }

        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }

    protected override void MutateApprovedPacket(ref BuyItemPacket packet, int peerId)
    {
        try
        {
            packet.item = packet.item.CloneItem();
            packet.placement = AU.GetItemPlacement(packet.item, packet.Player);
            packet.Timestamp = NetworkTime.ServerNowSeconds;

            if (packet.placement.Kind == PlacementKind.None)
            {
                SendRejection(ref packet, peerId, "Can't find placement for your item");
                return;
            }

            if (packet.item is Weapon weapon)
            {
                IU.DowngradeMagIfNeeded(weapon);
                RU.SetupWeapon(weapon, packet.Player); // will send off additional mag buyitem packets on its own for the client player
            }
            else if (packet.item is HeadwearItemClass headwear)
            {
                IU.AttachNightVisionIfNeeded(headwear);
            }
            else if (packet.item is ArmorPlateItemClass)
            {
                // server crashes somehow because of this afaik
                // int availablePlateSlots = packet.Player.CountAvailableArmorPlateSlots();
                // for (int i = 0; i < availablePlateSlots - 1; i++)  // auto fill other plate slots (buy one plate get all)
                // {
                //     var anotherPlatePacket = new BuyItemPacket
                //     {
                //         Player = packet.Player,
                //         item = packet.item,
                //     };
                //     DispatchPacket(ref anotherPlatePacket);
                // }
            }
        }
        catch (Exception ex)
        {
            D.Log(ex.StackTrace);
        }
    }

    protected override void Apply(BuyItemPacket packet, int peerId)
    {
        try
        {
            try
            {
                if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
                {
                    // we deduct money before sending it as a client
                    if (!packet.Player.IsYourPlayer) packet.Player.Context.SpendMoney(itemData.price);

                    packet.Player.Context.AddItemQuantity(itemData);
                }


                if (packet.placement.Kind == PlacementKind.EquipmentSlot && packet.item is Weapon)
                {
                    if (packet.Player.HandsController != null)
                    {
                        packet.Player.HandsController.FastForwardCurrentState();
                    }
                }

                SaveTransaction(packet);
            }
            catch (Exception ex)
            {
                D.Log(ex.StackTrace);
            }

            var success = packet.Player.PlaceItem(packet.item, packet.placement);
            // if (!success && H.IsClient)
            // {
            //     _resyncDebouncer.Debounce(
            //         packet.Player.Id,
            //         TimeSpan.FromMilliseconds(500),
            //         () =>
            //         {
            //             D.Notify($"Resynchronizing {packet.Player.Profile.Nickname}'s inventory");
            //             Singleton<EquipmentResyncPacketWarden>.Instance.Send(packet.Player);
            //         }
            //     );
            // }

        }
        catch (Exception ex)
        {
            D.Log(ex.StackTrace);
        }
    }

    protected override void WhenRejected(BuyItemPacket packet, int peerId)
    {
        if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
        {
            packet.Player.Context.AddMoney(itemData.price);
        }
    }

    // save the transaction in case the player requests a refund
    private void SaveTransaction(BuyItemPacket packet)
    {
        if (!RoundTransactions.TryGetValue(packet.ID, out var transactions))
        {
            transactions = new List<BuyItemPacket>();
            RoundTransactions[packet.ID] = transactions;
        }

        transactions.Add(packet);
    }
}