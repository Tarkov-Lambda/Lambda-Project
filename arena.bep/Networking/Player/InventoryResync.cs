using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Screens;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.Patches.Tarkov.UI;
using PacketHandler;
using PacketHandler.RateLimiting;
using System;

namespace ifp.arena.bep.networking;

public struct InventoryResyncPacket : INetSerializable
{
    public Player Player { get; set; }
    public InventoryDescriptorClass inventoryDescriptor;

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
    }

    public void Deserialize(NetDataReader reader)
    {
        Player = reader.GetPlayer();

        if (reader.GetBool())
        {
            inventoryDescriptor = reader.GetItemDescriptor();
        }
    }
}

public class InventoryResyncPacketHandler : PacketHandler<InventoryResyncPacket>
{
    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(5);

    public void Send()
    {
        var packet = new InventoryResyncPacket
        {
            Player = H.MainPlayer
        };

        if (packet.Player.IsYourPlayer)
        {
            Patch_EftGamePlayerOwner_TranslateInventoryScreenInput.AllowOpenInventory = false;
        }
        DispatchPacket(packet);
    }

    protected override void MutateApprovedPacket(ref InventoryResyncPacket packet, NetPeer peer)
    {
        packet.inventoryDescriptor = EFTItemSerializerClass.SerializeItem(packet.Player.Inventory.Equipment, Fika.Core.Main.Utils.FikaGlobals.SearchControllerSerializer);
    }

    protected override void ProcessApprovedPacket(ref InventoryResyncPacket packet, NetPeer peer)
    {
        MutateApprovedPacket(ref packet, peer);
        H.FikaNet.SendDataToPeer(ref packet, deliveryMethod, peer);
    }

    protected override void Apply(InventoryResyncPacket packet, NetPeer peer)
    {
        var player = packet.Player;
        if (player == null || packet.inventoryDescriptor == null) return;

        if (packet.Player.IsYourPlayer)
        {
            H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
            if (H.Session.matchState is not MatchState.Cleanup)
            {
                D.Notify("Resetting inventory, please wait...");
            }
            Patch_EftGamePlayerOwner_TranslateInventoryScreenInput.AllowOpenInventory = false;
        }

        // 4. Create and replace the inventory
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

        // 5. Update TraderControllerClass's root item via reflection 
        // (Without this, the controller rejects placing items in the new inventory)
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

        // 6. Refresh ItemUiContext bindings
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

        // 7. Rebind QuickAccessPanel (Fast panel hotkeys 1-0)
        // var battleUI = CurrentScreenSingletonClass.Instance.GetScreen<EftBattleUIScreen.GClass3865, BattleUIScreen<EftBattleUIScreen.GClass3865, EEftScreenType>>(EEftScreenType.BattleUI);
        // if (battleUI != null && battleUI.QuickAccessPanel != null)
        // {
        //     var gamePlayerOwner = player.gameObject.GetComponent<GamePlayerOwner>();
        //     battleUI.QuickAccessPanel.Show(player.InventoryController, ItemUiContext.Instance, gamePlayerOwner, ItemUiContext.Instance.Session?.InsuranceCompany);
        // }

        // 8. Rebuild PlayerBody visual slots (This is what actually draws the items on the character)
        // if (player.PlayerBody != null)
        // {
        //     foreach (var slotType in InventoryEquipment.AllSlotNames)
        //     {
        //         var slot = newInventory.Equipment.GetSlot(slotType);
        //         if (slot != null)
        //         {
        //             var slotBone = player.PlayerBody.GetSlotBone(slotType);
        //             var altHolsterBone = player.PlayerBody.GetAlternativeHolsterBone(slotType);
        //             var newSlotView = new PlayerBody.EquipmentSlotClass(
        //                 player.PlayerBody, 
        //                 slot, 
        //                 slotBone, 
        //                 slotType,
        //                 newInventory.Equipment.GetSlot(EquipmentSlot.Backpack), 
        //                 altHolsterBone, 
        //                 false);

        //             var oldSlotView = player.PlayerBody.SlotViews.AddOrReplace(slotType, newSlotView);

        //             // Safely dispose of old visual renderers
        //             if (oldSlotView != null)
        //             {
        //                 if (oldSlotView.Renderers != null)
        //                 {
        //                     foreach (var rndr in oldSlotView.Renderers)
        //                         if (rndr != null) rndr.forceRenderingOff = false;
        //                 }
        //                 if (oldSlotView.Dresses != null)
        //                 {
        //                     foreach (var dress in oldSlotView.Dresses)
        //                     {
        //                         var bodyRenderer = dress.GetBodyRenderer();
        //                         if (bodyRenderer.Renderers != null)
        //                         {
        //                             foreach (var rndr in bodyRenderer.Renderers)
        //                                 if (rndr != null) rndr.forceRenderingOff = false;
        //                         }
        //                     }
        //                 }
        //                 oldSlotView.Dispose();
        //             }
        //             player.PlayerBody.ValidateHoodedDress(slotType);
        //         }
        //     }
        //     player.PlayerBody.UpdatePlayerRenders(player.PointOfView, player.Side);
        //     // EFT.GlobalEvents.GlobalEventHandlerClass.Instance.CreateCommonEvent<GClass3558>().Invoke(player.ProfileId);
        // }

        if (packet.Player.IsYourPlayer)
        {
            H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
            Patch_EftGamePlayerOwner_TranslateInventoryScreenInput.AllowOpenInventory = true;
        }

        D.DumpFile(player.InventoryController, $"{player.Profile.Nickname}'s Replaced Inventory Controller", 3);
    }
}