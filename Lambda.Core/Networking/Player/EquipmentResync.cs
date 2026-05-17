using EFT;
using EFT.AssetsManager;
using EFT.InputSystem;
using EFT.InventoryLogic;
using EFT.UI;
using Fika.Core.Main.Utils;
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

[MemoryPackable]
public partial struct EquipmentResyncPacket : IPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    [MemoryPackAllowSerialize]
    public Dictionary<EquipmentSlot, ItemAndAddress> Equipment;

    public bool broadcast;
}

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

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(5);

    public void Send(Player player, bool broadcast = false)
    {
        var packet = new EquipmentResyncPacket
        {
            Player = player,
            broadcast = broadcast
        };

        DispatchPacket(packet);
    }

    public void SendToPeer(Player player, int peerId)
    {
        var packet = new EquipmentResyncPacket
        {
            Player = player,
            broadcast = false
        };

        DispatchPacket(packet, peerId);
    }

    protected override bool ValidatePacket(EquipmentResyncPacket packet, int peerId, out string rejectionReason)
    {
        if (packet.broadcast == true)
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
    }

    protected override void ProcessApprovedPacket(ref EquipmentResyncPacket packet, int peerId)
    {
        MutateApprovedPacket(ref packet, peerId);

        if (packet.broadcast)
        {
            PacketWardenUtils.Network.SendData(ref packet, DeliveryType, true);
            ApplyInternal(packet, peerId);
        }
        else if (peerId != Network.NetId)
        {
            PacketWardenUtils.Network.SendDataToPeer(ref packet, DeliveryType, peerId);
        }
    }

    protected override void Apply(EquipmentResyncPacket packet, int peerId)
    {
        Player player = packet.Player;

        if (player.HandsController != null)
        {
            try
            {
                player.ProcessStatus = EProcessStatus.None;
                player.AbstractProcess_0?.AbortAfterCompletion();
                player.AbstractProcess_0 = null;
                player.DestroyController();
            }
            catch (Exception ex)
            {
                D.LogError($"[EquipmentResync] Error safely destroying HandsController for {player.Profile.Nickname}: {ex}");
                
                // Fallback ensures no floating weapons are left on the map
                if (player.HandsController != null)
                {
                    if (player.HandsController.ControllerGameObject != null)
                    {
                        AssetPoolObject.ReturnToPool(player.HandsController.ControllerGameObject, true);
                    }
                    UnityEngine.Object.Destroy(player.HandsController);
                    player.HandsController = null;
                }
            }
        }

        player.RemoveLeftHandItem(1f);
        // player.ProceduralWeaponAnimation?.ClearPreviousWeapon();
        
        player.MovementContext?.SetBlindFire(0);

        foreach (var slotType in resyncableSlots)
        {
            Slot slot = player.Equipment.GetSlot(slotType);

            if (slot.ContainedItem != null)
            {
                var cachedItem = slot.ContainedItem;
                slot.RemoveItemWithoutRestrictions();
                var address = slot.CreateItemAddress();
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

        if (!H.IsHeadless)
        {
            player.ProcessStatus = EProcessStatus.None;
            player.SetFirstAvailableItem((result) => 
            {
                if (result.Failed)
                {
                    D.LogError($"[EquipmentResync] Failed to equip item: {result.Error}");
                    player.SetEmptyHands(delegate { });
                }
            });
        }
    }
}