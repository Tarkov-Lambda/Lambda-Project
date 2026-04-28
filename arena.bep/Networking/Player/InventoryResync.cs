using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Players;
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
using System.Threading;

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

        D.DumpFile(H.MainPlayer.InventoryController, $"{H.MainPlayer.Profile.Nickname}'s Original Inventory Controller", 3);

        DispatchPacket(packet);
    }

    protected override void MutateApprovedPacket(ref InventoryResyncPacket packet, NetPeer peer)
    {
        packet.inventoryDescriptor = EFTItemSerializerClass.SerializeItem(packet.Player.Inventory.Equipment, FikaGlobals.SearchControllerSerializer);
    }

    protected override void ProcessApprovedPacket(ref InventoryResyncPacket packet, NetPeer peer)
    {
        MutateApprovedPacket(ref packet, peer);
        H.FikaNet.SendDataToPeer(ref packet, deliveryMethod, peer);
    }

    protected override void Apply(InventoryResyncPacket packet, NetPeer peer)
    {
        var newInventory = new EFTInventoryClass()
        {
            Equipment = packet.inventoryDescriptor,
        }.ToInventory();


        packet.Player.InventoryController.ReplaceInventory(newInventory);
        newInventory.Equipment.CurrentAddress = packet.Player.InventoryController.CreateItemAddress();

        D.DumpFile(H.MainPlayer.InventoryController, $"{H.MainPlayer.Profile.Nickname}'s Replaced Inventory Controller", 3);
    }
}
