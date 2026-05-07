using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.InventoryLogic;
using EFT.UI;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.Core;
using PacketHandler;
using PacketHandler.RateLimiting;
using System;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.networking;

public struct InventoryResyncPacket : INetSerializable
{
    public Player Player { get; set; }
    public InventoryDescriptorClass inventoryDescriptor;
    public bool broadcast;

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPlayer(Player);
        if (inventoryDescriptor != null)
        {
            writer.Put(true);
            writer.PutItemDescriptor(inventoryDescriptor);
        }
        else
        {
            writer.Put(false);
        }

        writer.Put(broadcast);
    }

    public void Deserialize(NetDataReader reader)
    {
        Player = reader.GetPlayer();

        if (reader.GetBool())
        {
            inventoryDescriptor = reader.GetItemDescriptor();
        }

        broadcast = reader.GetBool();
    }
}

public class InventoryResyncPacketHandler : PacketHandler<InventoryResyncPacket>
{
    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(5);

    public void Send(Player player, bool broadcast = false)
    {
        var packet = new InventoryResyncPacket
        {
            Player = player,
            broadcast = broadcast
        };

        DispatchPacket(packet);
    }

    public void SendToPeer(Player player, NetPeer peer)
    {
        var packet = new InventoryResyncPacket
        {
            Player = player,
            broadcast = false
        };

        DispatchPacketToPeer(packet, peer);
    }

    protected override bool ValidatePacket(InventoryResyncPacket packet, NetPeer peer, out string rejectionReason)
    {
        if (packet.broadcast == true)
        {
            rejectionReason = "Action Unauthorized";
            return false;
        }

        return base.ValidatePacket(packet, peer, out rejectionReason);
    }

    protected override void MutateApprovedPacket(ref InventoryResyncPacket packet, NetPeer peer)
    {
        packet.inventoryDescriptor = EFTItemSerializerClass.SerializeItem(packet.Player.Inventory.Equipment, FikaGlobals.SearchControllerSerializer);
    }

    protected override void ProcessApprovedPacket(ref InventoryResyncPacket packet, NetPeer peer)
    {
        MutateApprovedPacket(ref packet, peer);

        if (packet.broadcast)
        {
            H.FikaNet.SendData(ref packet, DeliveryMethod, true);
        }
        else if (peer.Id != H.FikaNet.NetId)
        {
            H.FikaNet.SendDataToPeer(ref packet, DeliveryMethod, peer);
        }

        if (packet.Player.IsYourPlayer)
        {
            ApplyInternal(packet, peer);
        }
    }

    protected override void Apply(InventoryResyncPacket packet, NetPeer peer)
    {
        var player = packet.Player;
        if (packet.inventoryDescriptor == null) return;

        if (packet.Player.IsYourPlayer)
        {
            H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
            H.MainPlayer.UnfuckHands();
            if (H.Session.matchState is not MatchState.Cleanup)
            {
                D.Notify("Resetting inventory, please wait...");
            }
        }

        var newInventory = new EFTInventoryClass()
        {
            Equipment = packet.inventoryDescriptor,
        }.ToInventory();

        player.Profile.Inventory = newInventory;
        player.InventoryController.ReplaceInventory(newInventory);

        newInventory.Equipment.CurrentAddress = player.InventoryController.CreateItemAddress();
        if (newInventory.Stash != null)
        {
            newInventory.Stash.CurrentAddress = player.InventoryController.CreateItemAddress();
        }

        try
        {
            var rootItemField = AccessTools.Field(typeof(TraderControllerClass), "Item_0") ?? AccessTools.Field(typeof(TraderControllerClass), "RootItem");
            if (rootItemField != null)
            {
                rootItemField.SetValue(player.InventoryController, newInventory.Equipment);
            }
        }
        catch (Exception ex)
        {
            D.Log($"Failed to reflect TraderControllerClass root item: {ex}");
        }

        ReregisterPlayerVisuals(player, newInventory.Equipment);

        if (packet.Player.IsYourPlayer)
        {
            if (ItemUiContext.Instance != null)
            {
                ItemUiContext.Instance.Configure(
                    player.InventoryController,
                    player.Profile,
                    ItemUiContext.Instance.Session,
                    ItemUiContext.Instance.Session?.InsuranceCompany,
                    null,
                    player.HealthController,
                    ItemUiContext.Instance.CompoundItem_0,
                    ItemUiContext.Instance.ContextType,
                    ECursorResult.Ignore,
                    null,
                    newInventory.Equipment,
                    player.AbstractQuestControllerClass
                );
            }

            H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
        }

        if (!H.IsHeadless)
        {
            H.MainPlayer.AutoExamineAndSearch(packet.Player.Inventory.Equipment);
            // H.MainPlayer.AutoExamineAndSearch(player.GetSlotItem(EquipmentSlot.TacticalVest));
            // H.MainPlayer.AutoExamineAndSearch(player.GetSlotItem(EquipmentSlot.Pockets));
        }
    }

    // Lord forgive me
    private static void ReregisterPlayerVisuals(Player player, InventoryEquipment newEquipment)
    {
        UpdateObserver(player.NightVisionObserver, newEquipment.GetSlot(EquipmentSlot.Headwear));
        UpdateObserver(player.ThermalVisionObserver, newEquipment.GetSlot(EquipmentSlot.Headwear));
        UpdateObserver(player.FaceShieldObserver, newEquipment.GetSlot(EquipmentSlot.Headwear));
        UpdateObserver(player.FaceCoverObserver, newEquipment.GetSlot(EquipmentSlot.FaceCover));

        if (player.PlayerBody != null)
        {
            player.PlayerBody.Equipment = newEquipment;

            var backpackSlot = newEquipment.GetSlot(EquipmentSlot.Backpack);
            var slotNames = (EquipmentSlot[])AccessTools.Field(typeof(PlayerBody), "SlotNames").GetValue(null);
            var slotViews = player.PlayerBody.SlotViews;

            var getByKeyMethod = AccessTools.Method(slotViews.GetType(), "GetByKey");
            var addOrReplaceMethod = AccessTools.Method(slotViews.GetType(), "AddOrReplace");
            var equipmentSlotClassType = typeof(PlayerBody).GetNestedType("EquipmentSlotClass", BindingFlags.Public | BindingFlags.NonPublic);
            var disposeMethod = AccessTools.Method(equipmentSlotClassType, "Dispose");

            foreach (EquipmentSlot slotName in slotNames)
            {
                var newSlot = newEquipment.GetSlot(slotName);
                var oldSlotView = getByKeyMethod.Invoke(slotViews, [slotName]);

                Transform bone = null;
                Transform altBone = null;

                if (oldSlotView != null)
                {
                    bone = (Transform)AccessTools.Field(equipmentSlotClassType, "Transform_0").GetValue(oldSlotView);
                    altBone = (Transform)AccessTools.Field(equipmentSlotClassType, "Transform_1").GetValue(oldSlotView);
                }
                else
                {
                    bone = player.PlayerBody.GetSlotBone(slotName);
                    altBone = player.PlayerBody.GetAlternativeHolsterBone(slotName);
                }

                var newSlotView = Activator.CreateInstance(
                    equipmentSlotClassType,
                    [player.PlayerBody, newSlot, bone, slotName, backpackSlot, altBone, false]
                );

                var replacedView = addOrReplaceMethod.Invoke(slotViews, [slotName, newSlotView]);
                if (replacedView != null)
                {
                    disposeMethod.Invoke(replacedView, null);
                }
            }

            var disposeField = AccessTools.Field(typeof(PlayerBody), "_dispose");
            var compositeDisposable = disposeField?.GetValue(player.PlayerBody);

            if (compositeDisposable != null)
            {
                var addDisposableMethod = AccessTools.Method(compositeDisposable.GetType(), "AddDisposable", [typeof(Action)]);

                var headwearSlotView = getByKeyMethod.Invoke(slotViews, [EquipmentSlot.Headwear]);
                var faceCoverSlotView = getByKeyMethod.Invoke(slotViews, [EquipmentSlot.FaceCover]);

                var headwearParentedModel = AccessTools.Field(equipmentSlotClassType, "ParentedModel").GetValue(headwearSlotView);
                var faceCoverParentedModel = AccessTools.Field(equipmentSlotClassType, "ParentedModel").GetValue(faceCoverSlotView);

                var bindMethod = AccessTools.Method(headwearParentedModel.GetType(), "Bind");
                var method1Delegate = Delegate.CreateDelegate(typeof(Action<GameObject>), player.PlayerBody, "method_1");

                var hwDisposable = bindMethod.Invoke(headwearParentedModel, [method1Delegate]);
                var fcDisposable = bindMethod.Invoke(faceCoverParentedModel, [method1Delegate]);

                addDisposableMethod.Invoke(compositeDisposable, [hwDisposable]);
                addDisposableMethod.Invoke(compositeDisposable, [fcDisposable]);
            }

            AccessTools.Method(typeof(PlayerBody), "method_1").Invoke(player.PlayerBody, [null]);

            var method86Delegate = Delegate.CreateDelegate(typeof(Action<GameObject>), player, "method_86");
            player.BindSlotViewChangedAction(EquipmentSlot.Headwear, (Action<GameObject>)method86Delegate);
        }
    }

    private static void UpdateObserver(object observer, Slot newSlot)
    {
        if (observer == null || newSlot == null) return;

        var slotField = AccessTools.Field(observer.GetType(), "Slot_0");
        if (slotField != null)
        {
            slotField.SetValue(observer, newSlot);
        }

        var updateMethod = AccessTools.Method(observer.GetType(), "Update");
        if (updateMethod != null)
        {
            updateMethod.Invoke(observer, null);
        }
    }
}