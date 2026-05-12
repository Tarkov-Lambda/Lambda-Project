using System;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking.Pooling;
using MemoryPack;

// TODO: Make this work for all IItemOwner and not just InventoryController subtype
public class ItemAddressFormatter : MemoryPackFormatter<ItemAddress>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref ItemAddress value)
    {
        if (value == null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        Player addressPlayerOwner = null;
        foreach (var player in H.AllPlayers)
        {
            if (value.GetOwner() == player.InventoryController)
            {
                addressPlayerOwner = player;
                break;
            }
        }

        if (addressPlayerOwner == null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteUnmanaged(addressPlayerOwner.Id);

        var descriptor = value.ToDescriptor();
        var eftWriter = WriterPoolManager.GetWriter();

        eftWriter.WritePolymorph(descriptor);
        byte[] addressBytes = eftWriter.ToArray();

        WriterPoolManager.ReturnWriter(eftWriter);

        writer.WriteUnmanaged(addressBytes.Length);
        writer.WriteUnmanagedSpan((ReadOnlySpan<byte>)addressBytes);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref ItemAddress value)
    {
        int playerId = reader.ReadUnmanaged<int>();
        Player player = H.GetPlayer(playerId);

        int length = reader.ReadUnmanaged<int>();
        byte[] addressBytes = reader.ReadUnmanagedArray<byte>();

        if (addressBytes == null || addressBytes.Length == 0 || player == null)
        {
            value = null;
            return;
        }

        using var eftReader = PacketToEFTReaderAbstractClass.Get(addressBytes);

        var descriptor = eftReader.ReadPolymorph<GClass1950>();

        value = player.InventoryController.ToItemAddress(descriptor);
    }
}