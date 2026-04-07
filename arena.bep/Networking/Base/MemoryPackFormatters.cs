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

public static class MemoryPackHelper
{
    public static void Serialize<T>(NetDataWriter writer, T value)
    {
        writer.Put(MemoryPackSerializer.Serialize(value));
    }

    public static T Deserialize<T>(NetDataReader reader) where T : struct
    {
        int length = reader.AvailableBytes;
        byte[] bytes = new byte[length];
        reader.GetBytes(bytes, length);
        return MemoryPackSerializer.Deserialize<T>(bytes);
    }
}


public sealed class PlayerFormatter : MemoryPackFormatter<Player>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref Player value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteVarInt(value.Id);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref Player value)
    {
        if (reader.PeekIsNull())
        {
            reader.Advance(1);
            value = null;
            return;
        }

        var id = reader.ReadVarIntInt32();
        value = H.GetPlayer(id);
    }
}