using System;
using EFT.Interactive;
using MemoryPack;

public class WorldInteractiveObjectFormatter : MemoryPackFormatter<WorldInteractiveObject>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref WorldInteractiveObject value)
    {
        if (value == null || value?.NetId == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(1);
        writer.WriteUnmanaged(value.NetId);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref WorldInteractiveObject value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = null;
            return;
        }

        value.NetId = reader.ReadUnmanaged<int>();
    }
}
