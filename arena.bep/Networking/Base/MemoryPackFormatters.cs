using EFT;
using Fika.Core.Networking.LiteNetLib.Utils;
using MemoryPack;

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


public class PlayerFormatter : MemoryPackFormatter<Player>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref Player value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(1);
        writer.WriteUnmanaged(value.Id);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref Player value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = null;
            return;
        }

        int id = reader.ReadUnmanaged<int>();
        value = H.GetPlayer(id);
    }
}