using EFT.Interactive;
using MemoryPack;

public class WorldInteractiveObjectFormatter : MemoryPackFormatter<WorldInteractiveObject>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer,
        scoped ref WorldInteractiveObject value
    )
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(1);
        writer.WriteString(value.Id);
    }

    public override void Deserialize(
        ref MemoryPackReader reader,
        scoped ref WorldInteractiveObject value
    )
    {
        if (!reader.TryReadObjectHeader(out _))
        {
            value = null;
            return;
        }

        string id = reader.ReadString();
        value = H.IsInRaid() && id != null ? H.GameWorld.FindDoor(id) : null;
    }
}
