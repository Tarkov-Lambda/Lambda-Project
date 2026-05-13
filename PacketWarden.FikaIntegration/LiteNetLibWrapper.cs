using System;
using System.Buffers;
using Fika.Core.Networking.LiteNetLib.Utils;
using MemoryPack;

internal static class LiteNetLibWrapperShared
{
    [ThreadStatic]
    public static ArrayBufferWriter<byte> SharedWriter;
}

internal struct LiteNetLibWrapper<T> : INetSerializable where T : IPacket
{
    public T Payload;

    public readonly void Serialize(NetDataWriter writer)
    {
        LiteNetLibWrapperShared.SharedWriter ??= new ArrayBufferWriter<byte>(1024);
        var w = LiteNetLibWrapperShared.SharedWriter;
        w.Clear();

        MemoryPackSerializer.Serialize(w, Payload);

        writer.Put(w.WrittenCount);
        writer.Put(w.WrittenSpan);
    }

    public void Deserialize(NetDataReader reader)
    {
        int length = reader.GetInt();

        ReadOnlySpan<byte> span = new(reader.RawData, reader.Position, length);

        reader.SkipBytes(length);

        Payload = MemoryPackSerializer.Deserialize<T>(span);
    }
}