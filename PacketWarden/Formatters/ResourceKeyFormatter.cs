using System;
using EFT;
using EFT.Interactive;
using MemoryPack;

public class ResourceKeyFormatter : MemoryPackFormatter<ResourceKey>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref ResourceKey value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(2);
        writer.WriteString(value.path);
        writer.WriteString(value.rcid);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref ResourceKey value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = null;
            return;
        }

        value.path = reader.ReadString();
        value.rcid = reader.ReadString();
    }
}
