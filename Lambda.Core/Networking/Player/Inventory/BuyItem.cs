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

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct BuyItemPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    [MemoryPackAllowSerialize]
    public ItemPlacement placement;

    [MemoryPackAllowSerialize]
    public Item item;
}

// all the inventory add logic should just be rewritten from scratch
public class BuyItemPacketWarden : LambdaPacketWarden<BuyItemPacket>
{
    private readonly KeyedDebouncer<int> _resyncDebouncer = new();

    protected override bool ShouldNotifyAboutRejection => true;
    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(0.15);

    public BuyItemPacketWarden()
    {
        // EventBus.OnEnter += OnMatchStateEnter;
    }

    public override void Dispose()
    {
        // EventBus.OnEnter -= OnMatchStateEnter;

        _resyncDebouncer.Dispose();
        base.Dispose();
    }

    // public void OnMatchStateEnter(MatchState state)
    // {
    //     if (state is MatchState.Cleanup)
    //         _resyncDebouncer.Dispose();
    // }

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
                var playerScore = packet.Player.GetContext();

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

            if (packet.item is HeadwearItemClass)
            {
                if (packet.Player.CountAvailableArmorPlateSlots() > 0)
                {
                    rejectionReason = $"You must buy armor first";
                    return false;
                }
            }
        }

        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }


    // // SERVER: Wait for the host's representation of the player's inventory to settle, then mutate and broadcast.
    // protected override void ProcessApprovedPacket(ref BuyItemPacket packet, int peerId)
    // {
    //     int playerId = packet.Player.Id;
    //     var localPacket = packet;

    //     PlayerInventoryTimeGate.Enqueue(playerId, () =>
    //     {
    //         MutateApprovedPacket(ref localPacket, peerId); // THIS CAN REJECT IF PLACEMENT IS NOT FOUND
    //         if (localPacket.placement.Kind == PlacementKind.None)
    //         {
    //             SendRejection(ref localPacket, peerId, "Can't find placement for your item");
    //             return;
    //         }

    //         PacketWardenUtils.Network.SendData(ref localPacket, DeliveryType, true);
    //         ApplyInternal(localPacket, peerId);
    //     });
    // }

    // // CLIENT: Wait for the observing client's representation of the player's inventory to settle, then apply locally.
    // // I don't know if this will play nicely, but it's "good enough" for now
    // protected override void WhenClientReceivesPacket(BuyItemPacket packet, int peerId)
    // {
    //     int playerId = packet.Player.Id;
    //     var localPacket = packet;

    //     PlayerInventoryTimeGate.Enqueue(playerId, () => base.WhenClientReceivesPacket(localPacket, peerId));
    // }

    protected override void MutateApprovedPacket(ref BuyItemPacket packet, int peerId)
    {
        try
        {
            packet.item = packet.item.CloneItem();
            packet.placement = AU.GetItemPlacement(packet.item, packet.Player);

            if (packet.placement.Kind == PlacementKind.None)
            {
                SendRejection(ref packet, peerId, "Can't find placement for your item");
                return;
            }

            // this logic needs to be relocated
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
            if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
            {
                // we deduct money before sending it as a client
                if (!packet.Player.IsYourPlayer)
                    H.GetPlayerScore(packet.Player.Id).SpendMoney(itemData.price);

                packet.Player.GetContext().AddItemQuantity(itemData);
            }

            if (packet.placement.Kind == PlacementKind.EquipmentSlot && packet.item is Weapon)
            {
                if (packet.Player.HandsController != null)
                {
                    packet.Player.HandsController.FastForwardCurrentState();
                }
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
            H.GetPlayerScore(packet.Player.Id).AddMoney(itemData.price);
        }
    }
}