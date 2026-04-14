using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Pooling;
using MemoryPack;

namespace ifp.arena.bep.networking;

public static class TarkovSerializationExtension
{
    public static void PutPlayer(this NetDataWriter writer, Player player)
    {
        writer.Put(player.Id);
    }

    public static Player GetPlayer(this NetDataReader reader)
    {
        var playerId = reader.GetString();
        return H.GetPlayer(playerId);
    }

    public static void Put(this NetDataWriter writer, ItemAddress itemAddress)
    {
        GClass1950 descriptor = itemAddress.ToDescriptor();
        EFTWriterClass eftWriter = WriterPoolManager.GetWriter();
        eftWriter.WritePolymorph(descriptor);
        byte[] _addressDescriptor = eftWriter.ToArray();
        WriterPoolManager.ReturnWriter(eftWriter);
        writer.PutByteArray(_addressDescriptor);
    }

    public static ItemAddress GetItemAddress(this NetDataReader reader, Player player)
    {
        byte[] _addressDescriptor = reader.GetByteArray();
        using var eftReader = PacketToEFTReaderAbstractClass.Get(_addressDescriptor);
        var descriptor = eftReader.ReadPolymorph<GClass1950>();
        return player.InventoryController.ToItemAddress(descriptor);
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