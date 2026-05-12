using System;
using System.Buffers;
using Fika.Core.Networking.LiteNetLib.Utils;
using MemoryPack;

public struct LiteNetLibWrapper<T> : INetSerializable where T : IPacket
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte> _sharedWriter;

    public T Payload;

    public readonly void Serialize(NetDataWriter writer)
    {
        _sharedWriter ??= new ArrayBufferWriter<byte>(1024);
        _sharedWriter.Clear();

        MemoryPackSerializer.Serialize(_sharedWriter, Payload);

        writer.Put(_sharedWriter.WrittenSpan);
    }

    public void Deserialize(NetDataReader reader)
    {
        ReadOnlySpan<byte> span = reader.GetRemainingBytesSpan();

        T value = MemoryPackSerializer.Deserialize<T>(span);

        reader.SkipBytes(reader.AvailableBytes);

        Payload = value;
    }
}