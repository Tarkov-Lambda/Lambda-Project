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
using static EFT.Player;

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

        // D.Log(writer.Length.ToString());
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
    readonly FieldInfo traderControllerClassItem_0FieldInfo = AccessTools.Field(typeof(TraderControllerClass), "Item_0");

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
        newInventory.Stash?.CurrentAddress = player.InventoryController.CreateItemAddress();

        traderControllerClassItem_0FieldInfo.SetValue(player.InventoryController, newInventory.Equipment);

        player.UpdateVisuals(newInventory.Equipment);

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
            if (packet.Player.IsYourPlayer && H.Session.matchState == MatchState.Cleanup)
            {
                player.ProcessStatus = EProcessStatus.None;
                player.SetFirstAvailableItem((result) => { });
            }
        }
    }
}