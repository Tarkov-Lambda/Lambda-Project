using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Economy;
using PacketHandler;
using ifp.arena.shared.Models;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using System.Linq;
using PacketHandler.RateLimiting;

namespace ifp.arena.bep.networking;

public struct BuyItemPacket : INetSerializable, IAuthoredPacket
{
    public Player Player { get; set; }
    public ItemPlacement placement;
    public Item item;

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPlayer(Player);
        writer.Put(placement);
        writer.PutItem(item);
    }

    public void Deserialize(NetDataReader reader)
    {
        Player = reader.GetPlayer();
        placement = reader.GetItemPlacement(Player);
        item = reader.GetItem();
    }
}

public class BuyItemPacketHandler : PacketHandler<BuyItemPacket>
{
    protected override bool ShouldNotifyAboutRejection => true;

    protected override bool ShouldProcessInstantly => false;

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(0.1);

    public void Send(Item item, ItemPlacement placement, Player player)
    {
        var packet = new BuyItemPacket
        {
            Player = player,
            item = item, // this item is only a template, the server clones it before application
            placement = placement,
        };

        DispatchPacket(packet);
    }

    protected override bool ValidatePacket(BuyItemPacket packet, NetPeer peer, out string rejectionReason)
    {
        if (H.Session.matchState != MatchState.Cleanup)
        {
            if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
            {
                var playerScore = packet.Player.GetScore();

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
        }

        return base.ValidatePacket(packet, peer, out rejectionReason);
    }


    // SERVER: Wait for the host's representation of the player's inventory to settle, then mutate and broadcast.
    protected override void ProcessApprovedPacket(ref BuyItemPacket packet, NetPeer peer)
    {
        int playerId = packet.Player.Id;
        var localPacket = packet;

        PlayerInventoryTimeGate.Enqueue(playerId, () =>
        {
            MutateApprovedPacket(ref localPacket, peer); // THIS CAN REJECT IF PLACEMENT IS NOT FOUND
            if (localPacket.placement.Kind == PlacementKind.None) return;
            
            H.FikaNet.SendData(ref localPacket, DeliveryMethod, true);
            ApplyInternal(localPacket, peer);
        });
    }

    // CLIENT: Wait for the observing client's representation of the player's inventory to settle, then apply locally.
    // I don't know if this will play nicely, but it's "good enough" for now
    protected override void WhenClientReceivesPacket(BuyItemPacket packet, NetPeer peer)
    {
        int playerId = packet.Player.Id;
        var localPacket = packet;

        PlayerInventoryTimeGate.Enqueue(playerId, () => base.WhenClientReceivesPacket(localPacket, peer));
    }

    protected override void MutateApprovedPacket(ref BuyItemPacket packet, NetPeer peer)
    {
        packet.item = packet.item.CloneItem();
        packet.placement = AU.GetItemPlacement(packet.item, packet.Player);

        if (packet.placement.Kind == PlacementKind.None)
        {
            SendRejection(ref packet, peer, "Can't find placement for your item");
            return;
        }

        if (packet.item is Weapon weapon)
        {
            if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData) && itemData.maxMagSize != 0)
            {
                var magSlot = weapon.GetMagazineSlot();
                MagazineItemClass mag = magSlot.ContainedItem as MagazineItemClass;

                bool needsReplacement = mag == null
                    || mag.Cartridges.MaxCount > itemData.maxMagSize
                    || mag.Cartridges.Items.Any(cartridge => cartridge.TemplateId != itemData.ammoId);

                if (needsReplacement)
                {
                    WeaponBuildClass defaultPresetWeaponBuild = FU.Presets.FirstOrDefault(b => b.FromPreset && b.Item.TemplateId == weapon.TemplateId);
                    Weapon defaultPresetWeapon = defaultPresetWeaponBuild.Item as Weapon;

                    MagazineItemClass defaultWeaponMag = defaultPresetWeapon.GetCurrentMagazine().CloneItem();

                    if (mag != null)
                    {
                        magSlot.RemoveItemWithoutRestrictions();
                    }
                    magSlot.AddWithoutRestrictions(defaultWeaponMag);
                }
            }

            RU.SetupWeapon(weapon, packet.Player);
        }
        else if (packet.item is HeadwearItemClass headwear)
        {
            IU.AttachNightVisionIfNeeded(headwear);
        }
    }

    protected override void Apply(BuyItemPacket packet, NetPeer peer)
    {
        if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
        {
            H.GetPlayerScore(packet.Player.Id).SpendMoney(itemData.price);

            packet.Player.GetScore().AddItemQuantity(itemData);
        }

        if (packet.placement.Kind == PlacementKind.EquipmentSlot && packet.item is Weapon)
        {
            packet.Player.HandsController?.FastForwardCurrentState();
        }

        packet.Player.PlaceItem(packet.item, packet.placement);
    }

    protected override void WhenRejected(BuyItemPacket packet, NetPeer peer)
    {
        if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
        {
            H.GetPlayerScore(packet.Player.Id).AddMoney(itemData.price);
        }
    }
}

public static class PlayerInventoryTimeGate
{
    private static readonly ConcurrentDictionary<int, PlayerQueue> _playerQueues = new();

    public static void Enqueue(int playerId, Action action)
    {
        var queue = _playerQueues.GetOrAdd(playerId, id => new PlayerQueue(id));
        queue.Enqueue(action);
    }

    public static void ClearAll()
    {
        _playerQueues.Clear();
    }

    private class PlayerQueue(int playerId)
    {
        private readonly int _playerId = playerId;
        private readonly ConcurrentQueue<Action> _queue = new();
        private int _isProcessing = 0; // 0/1 false/true

        public void Enqueue(Action action)
        {
            _queue.Enqueue(action);

            // if the loop isn't running - start it
            if (Interlocked.Exchange(ref _isProcessing, 1) == 0)
            {
                ProcessQueueAsync().Forget();
            }
        }

        private async UniTaskVoid ProcessQueueAsync()
        {
            try
            {
                while (_queue.TryDequeue(out var action))
                {
                    Player player = H.GetPlayer(_playerId);

                    if (player != null && player.InventoryController is TraderControllerClass traderController)
                    {
                        float timeout = 3.0f;
                        while (traderController.HasActiveEvents && timeout > 0)
                        {
                            timeout -= Time.deltaTime;
                        }
                    }

                    await UniTask.DelayFrame(1);

                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        D.LogError($"[PlayerInventoryTimeGate] Error processing packet: {ex}");
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);

                if (!_queue.IsEmpty && Interlocked.Exchange(ref _isProcessing, 1) == 0)
                {
                    ProcessQueueAsync().Forget();
                }
            }
        }
    }
}