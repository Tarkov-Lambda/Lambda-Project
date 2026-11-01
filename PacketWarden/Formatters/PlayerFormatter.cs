using System.Collections.Generic;
using EFT;
using MemoryPack;

// Player.Id is the Fika NetId and is identical on every peer.
public class PlayerFormatter : MemoryPackFormatter<Player>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer,
        scoped ref Player value
    )
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
        if (!reader.TryReadObjectHeader(out _))
        {
            value = null;
            return;
        }

        value = Find(reader.ReadUnmanaged<int>());
    }

    public static Player Find(int id)
    {
        if (!H.IsInRaid())
            return null;

        List<Player> players = H.GameWorld.AllAlivePlayersList;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == id)
                return players[i];
        }

        return null;
    }
}
