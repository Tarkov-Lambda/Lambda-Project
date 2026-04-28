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
using ifp.arena.bep.Core;
using ifp.arena.bep.Patches.Tarkov.UI;
using PacketHandler;
using PacketHandler.RateLimiting;
using System;

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

        if (packet.Player.IsYourPlayer)
        {
            Patch_EftGamePlayerOwner_TranslateInventoryScreenInput.AllowOpenInventory = false;
        }

        DispatchPacket(packet);
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
        packet.inventoryDescriptor = EFTItemSerializerClass.SerializeItem(packet.Player.Inventory.Equipment, Fika.Core.Main.Utils.FikaGlobals.SearchControllerSerializer);
    }

    protected override void ProcessApprovedPacket(ref InventoryResyncPacket packet, NetPeer peer)
    {
        MutateApprovedPacket(ref packet, peer);
        if (packet.broadcast)
        {
            H.FikaNet.SendData(ref packet, deliveryMethod, true);
        }
        else
        {
            H.FikaNet.SendDataToPeer(ref packet, deliveryMethod, peer);
        }
    }

    protected override void Apply(InventoryResyncPacket packet, NetPeer peer)
    {
        var player = packet.Player;
        if (packet.inventoryDescriptor == null) return;

        if (packet.Player.IsYourPlayer)
        {
            H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
            if (H.Session.matchState is not MatchState.Cleanup)
            {
                D.Notify("Resetting inventory, please wait...");
                H.MainPlayer.SetEmptyHands(delegate { });
            }
            Patch_EftGamePlayerOwner_TranslateInventoryScreenInput.AllowOpenInventory = false;
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
            Patch_EftGamePlayerOwner_TranslateInventoryScreenInput.AllowOpenInventory = true;
        }

        if (!H.IsHeadless)
        {
            H.MainPlayer.AutoExamineAndSearch(packet.Player.Inventory.Equipment);
        }

        // D.DumpFile(player.InventoryController, $"{player.Profile.Nickname}'s Replaced Inventory Controller", 3);
    }
}