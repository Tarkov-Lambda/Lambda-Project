using EFT;
using MemoryPack;

public class ResourceKeyFormatter : MemoryPackFormatter<ResourceKey>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer,
        scoped ref ResourceKey value
    )
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
        if (!reader.TryReadObjectHeader(out _))
        {
            value = null;
            return;
        }

        value = new ResourceKey { path = reader.ReadString(), rcid = reader.ReadString() };
    }
}
