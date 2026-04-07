using System;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Pooling;
using ifp.arena.bep.Core;
using MemoryPack;
using Newtonsoft.Json;
using UnityEngine;

namespace ifp.arena.bep.networking;


public static class NetExtensions
{
    public static void Put(this NetDataWriter writer, Vector3 v)
    {
        writer.Put(v.x);
        writer.Put(v.y);
        writer.Put(v.z);
    }

    public static Vector3 GetVector3(this NetDataReader reader)
    {
        return new Vector3(
            reader.GetFloat(),
            reader.GetFloat(),
            reader.GetFloat()
        );
    }

    public static void PutPlayer(this NetDataWriter writer, Player player)
    {
        writer.Put(player.Id);
    }

    public static Player GetPlayer(this NetDataReader reader)
    {
        var playerId = reader.GetInt();
        return H.GetPlayer(playerId);
    }

    public static void Put(this NetDataWriter writer, ItemPlacement placement)
    {
        writer.Put((int)placement.Slot);
        writer.Put((int)placement.Kind);

        writer.Put(placement.Address);
    }

    public static ItemPlacement GetItemPlacement(this NetDataReader reader, Player player)
    {
        var placementSlot = (EquipmentSlot)reader.GetInt();
        var placementKind = (PlacementKind)reader.GetInt();
        ItemAddress address = reader.GetItemAddress(player);

        return new ItemPlacement(placementKind, placementSlot, address);
    }

    public static void Put(this NetDataWriter writer, ItemAddress itemAddress)
    {
        GClass1950 descriptor = itemAddress.ToDescriptor();
        EFTWriterClass eftWriter = WriterPoolManager.GetWriter();
        eftWriter.WritePolymorph(descriptor);
        byte[] _addressDescriptor = eftWriter.ToArray();
        WriterPoolManager.ReturnWriter(eftWriter);
        writer.PutByteArray(_addressDescriptor);
    }

    public static ItemAddress GetItemAddress(this NetDataReader reader, Player player)
    {
        byte[] _addressDescriptor = reader.GetByteArray();
        using var eftReader = PacketToEFTReaderAbstractClass.Get(_addressDescriptor);
        var descriptor = eftReader.ReadPolymorph<GClass1950>();
        return player.InventoryController.ToItemAddress(descriptor);
    }

    public static void Put(this NetDataWriter writer, Quaternion q)
    {
        writer.Put(q.x);
        writer.Put(q.y);
        writer.Put(q.z);
        writer.Put(q.w);
    }

    public static Quaternion GetQuaternion(this NetDataReader reader)
    {
        return new Quaternion(
            reader.GetFloat(),
            reader.GetFloat(),
            reader.GetFloat(),
            reader.GetFloat()
        );
    }

}