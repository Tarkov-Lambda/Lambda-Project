using System;
using System.Buffers;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using MemoryPack;

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

        var segment = eftWriter.ToArraySegment();
        writer.WriteUnmanagedSpan(segment.AsSpan());
        
        WriterPoolManager.ReturnWriter(eftWriter);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref ItemAddress value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = null;
            return;
        }

        int playerId = reader.ReadUnmanaged<int>();

        Span<byte> span = default;
        reader.ReadUnmanagedSpan(ref span);

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

        if (span.Length == 0 || player == null)
        {
            value = null;
            return;
        }

        byte[] rentedBytes = ArrayPool<byte>.Shared.Rent(span.Length);
        try
        {
            span.CopyTo(rentedBytes);

            var segment = new ArraySegment<byte>(rentedBytes, 0, span.Length);
            using var eftReader = PacketToEFTReaderAbstractClass.Get(segment);
            var descriptor = eftReader.ReadPolymorph<GClass1950>();
            value = player.InventoryController.ToItemAddress(descriptor);
        }
        catch (Exception) 
        { 
            value = null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBytes);
        }
    }
}