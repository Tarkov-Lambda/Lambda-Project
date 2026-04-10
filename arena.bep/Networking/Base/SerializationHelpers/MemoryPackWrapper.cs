using Fika.Core.Networking.LiteNetLib.Utils;
using MemoryPack;
using System;
using System.Buffers;

namespace ifp.arena.bep.networking;

public static class MemoryPackWrapper
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte> _sharedWriter;

    public static void Serialize<T>(NetDataWriter writer, T value)
    {
        _sharedWriter ??= new ArrayBufferWriter<byte>(1024);
        _sharedWriter.Clear();

        MemoryPackSerializer.Serialize(_sharedWriter, value);

        writer.Put(_sharedWriter.WrittenSpan);
    }

    public static T Deserialize<T>(NetDataReader reader)
    {
        ReadOnlySpan<byte> span = reader.GetRemainingBytesSpan();
        
        T value = MemoryPackSerializer.Deserialize<T>(span);

        reader.SkipBytes(reader.AvailableBytes);
        
        return value;
    }
}