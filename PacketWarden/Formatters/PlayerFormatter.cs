using Comfort.Common;
using EFT;
using MemoryPack;

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

        if (!Singleton<GameWorld>.Instantiated)
        {
            value = null;
            return;
        }
        
        foreach (var player in Singleton<GameWorld>.Instance.AllAlivePlayersList)
        {
            if (player.Id == id)
            {
                value = player;
                break;
            }
        }
    }
}