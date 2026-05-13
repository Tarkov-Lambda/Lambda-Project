using System;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using MemoryPack;

// TODO: Make this work for all IItemOwner and not just InventoryController subtype
public class ItemAddressFormatter : MemoryPackFormatter<ItemAddress>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref ItemAddress value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        Player addressPlayerOwner = null;
        foreach (var player in Singleton<GameWorld>.Instance.AllAlivePlayersList)
        {
            if (value.GetOwner().RootItem == player.InventoryController.RootItem)
            {
                addressPlayerOwner = player;
                break;
            }
        }

        if (addressPlayerOwner == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(2);
        writer.WriteUnmanaged(addressPlayerOwner.Id);

        var descriptor = value.ToDescriptor();
        
        var eftWriter = WriterPoolManager.GetWriter();

        eftWriter.WritePolymorph(descriptor);

        byte[] addressBytes = eftWriter.ToArray();
        
        WriterPoolManager.ReturnWriter(eftWriter);

        writer.WriteUnmanagedArray(addressBytes);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref ItemAddress value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = null;
            return;
        }

        int playerId = reader.ReadUnmanaged<int>();
        byte[] addressBytes = reader.ReadUnmanagedArray<byte>();

        if (!Singleton<GameWorld>.Instantiated)
        {
            value = null;
            return;
        }

        Player player = null;
        foreach (Player alivePlayer in Singleton<GameWorld>.Instance.AllAlivePlayersList)
        {
            if (alivePlayer.Id == playerId)
            {
                player = alivePlayer;
                break;
            }
        }

        if (addressBytes == null || addressBytes.Length == 0 || player == null)
        {
            value = null;
            return;
        }

        try
        {
            using var eftReader = PacketToEFTReaderAbstractClass.Get(addressBytes);
            var descriptor = eftReader.ReadPolymorph<GClass1950>();
            value = player.InventoryController.ToItemAddress(descriptor);
        }
        catch (Exception) { }
    }
}