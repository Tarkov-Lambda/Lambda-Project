using System;
using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using MemoryPack;

// TODO: Make this work for all IItemOwner and not just InventoryController subtype
public class ItemAddressFormatter : MemoryPackFormatter<ItemAddress>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref ItemAddress value)
    {
        Player owner = value != null ? FindOwner(value) : null;

        if (owner == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(2);
        writer.WriteUnmanaged(owner.Id);

        var eftWriter = WriterPoolManager.GetWriter();
        eftWriter.WritePolymorph(value.ToDescriptor());
        WriterPoolManager.WriteBlob(ref writer, eftWriter);
        WriterPoolManager.ReturnWriter(eftWriter);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref ItemAddress value)
    {
        value = null;

        if (!reader.TryReadObjectHeader(out _)) return;

        Player player = PlayerFormatter.Find(reader.ReadUnmanaged<int>());

        if (!WriterPoolManager.TryReadBlob(ref reader, out ArraySegment<byte> blob) || player == null) return;

        try
        {
            using var eftReader = PacketToEFTReaderAbstractClass.Get(blob);
            value = player.InventoryController.ToItemAddress(eftReader.ReadPolymorph<GClass1950>());
        }
        catch (Exception) { }
    }

    private static Player FindOwner(ItemAddress address)
    {
        if (!H.IsInRaid()) return null;

        Item rootItem = address.GetOwner()?.RootItem;
        if (rootItem == null) return null;

        List<Player> players = H.GameWorld.AllAlivePlayersList;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].InventoryController.RootItem == rootItem) return players[i];
        }

        return null;
    }
}