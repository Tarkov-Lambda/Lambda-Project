using EFT;
using EFT.AssetsManager;
using EFT.InventoryLogic;
using MemoryPack;
using PacketWarden.RateLimiting;
using System;
using System.Collections.Generic;
using static EFT.Player;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct ItemAndAddress
{
    [MemoryPackAllowSerialize]
    public Item item;

    [MemoryPackAllowSerialize]
    public ItemAddress address;
}

public enum EquipmentResyncRequestType : byte
{
    CleanupBroadcast,
    ClientRequest
}

[MemoryPackable]
public partial struct EquipmentResyncPacket : IPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    [MemoryPackAllowSerialize]
    public Dictionary<EquipmentSlot, ItemAndAddress> Equipment;

    [MemoryPackAllowSerialize]
    public Item WeaponInHands;

    public EquipmentResyncRequestType type;
}

// TODO: include optional item in hands address for clients if this is requested in round
public class EquipmentResyncPacketWarden : LambdaPacketWarden<EquipmentResyncPacket>
{
    private readonly EquipmentSlot[] resyncableSlots = [
        EquipmentSlot.Earpiece,
        EquipmentSlot.Headwear,
        EquipmentSlot.FaceCover,
        EquipmentSlot.Eyewear,
        EquipmentSlot.ArmorVest,
        EquipmentSlot.TacticalVest,
        EquipmentSlot.FirstPrimaryWeapon,
        EquipmentSlot.SecondPrimaryWeapon,
        EquipmentSlot.Holster,
        EquipmentSlot.Backpack,
        EquipmentSlot.Scabbard,
        EquipmentSlot.Pockets,
    ];

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(0.1, RateLimitAction.Drop);

    public void Send(Player player, EquipmentResyncRequestType type = EquipmentResyncRequestType.ClientRequest)
    {
        var packet = new EquipmentResyncPacket
        {
            Player = player,
            type = type
        };

        DispatchPacket(ref packet);
    }

    public void SendToPeer(Player player, int peerId)
    {
        var packet = new EquipmentResyncPacket
        {
            Player = player,
            type = EquipmentResyncRequestType.ClientRequest
        };

        DispatchPacket(ref packet, peerId);
    }

    protected override bool ValidatePacket(EquipmentResyncPacket packet, int peerId, out string rejectionReason)
    {
        if (packet.type is EquipmentResyncRequestType.CleanupBroadcast)
        {
            rejectionReason = "Action Unauthorized";
            return false;
        }

        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }

    protected override void MutateApprovedPacket(ref EquipmentResyncPacket packet, int peerId)
    {
        packet.Equipment = new();
        foreach (var slot in resyncableSlots)
        {
            var item = packet.Player.GetSlotItem(slot);
            if (item != null)
            {
                packet.Equipment[slot] = new ItemAndAddress
                {
                    item = item,
                    address = item.CurrentAddress
                };
            }
        }

        if (H.Session.matchState is not MatchState.Cleanup && packet.type is EquipmentResyncRequestType.ClientRequest)
        {
            if (packet.Player.HandsController.Item is not null and Weapon weapon)
            {
                packet.WeaponInHands = weapon;
            }
        }
    }

    protected override void ProcessApprovedPacket(ref EquipmentResyncPacket packet, int peerId)
    {
        MutateApprovedPacket(ref packet, peerId);

        if (packet.type is EquipmentResyncRequestType.CleanupBroadcast)
        {
            Network.SendData(ref packet, DeliveryType, true);
            ApplyInternal(packet, peerId);
        }
        else if (peerId is not INetworkBackend.LocalPeerId)
        {
            Network.SendDataToPeer(ref packet, DeliveryType, peerId);
        }
    }

    protected override void Apply(EquipmentResyncPacket packet, int peerId)
    {
        Player player = packet.Player;

        player.UnfuckHands();

        foreach (var slotType in resyncableSlots)
        {
            Slot slot = player.Equipment.GetSlot(slotType);

            if (slot.ContainedItem != null)
            {
                var cachedItem = slot.ContainedItem;
                var address = cachedItem.CurrentAddress;
                slot.RemoveItemWithoutRestrictions();
                address.RaiseForceRemove(cachedItem, player);
            }

            if (packet.Equipment.TryGetValue(slotType, out var itemAndAddress))
            {
                slot.AddWithoutRestrictions(itemAndAddress.item);
                itemAndAddress.address.RaiseForceAdd(itemAndAddress.item, player);

                if (player.IsYourPlayer)
                {
                    player.AutoExamineAndSearch(itemAndAddress.item);
                }
            }
        }

        player.ProcessStatus = EProcessStatus.None;

        if (packet.WeaponInHands != null)
        {
            player.SetInHands(packet.WeaponInHands, (result) =>
            {
                if (result.Failed)
                {
                    D.LogError($"[EquipmentResync] Failed to equip item: {result.Error}");
                    player.SetEmptyHands(delegate { });
                }
            });

            return;
        }

        if (player.IsYourPlayer)
        {
            player.SetFirstAvailableItem((result) =>
            {
                if (result.Failed)
                {
                    D.LogError($"[EquipmentResync] Failed to equip item: {result.Error}");
                    player.SetEmptyHands(delegate { });
                }
            });
        }
        else
        {
            player.SetEmptyHands(delegate { });
        }
    }
}