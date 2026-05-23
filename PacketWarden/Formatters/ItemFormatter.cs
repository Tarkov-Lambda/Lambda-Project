using System;
using System.Buffers;
using Comfort.Common;
using EFT.InventoryLogic;
using MemoryPack;

public class ItemFormatter : MemoryPackFormatter<Item>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref Item value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(1);
        
        var descriptor = EFTItemSerializerClass.SerializeItem(value, GClass2240.Instance);

        var eftWriter = WriterPoolManager.GetWriter();
        eftWriter.WriteEFTItemDescriptor(descriptor);

        var segment = eftWriter.ToArraySegment();
        writer.WriteUnmanagedSpan(segment.AsSpan());
        
        WriterPoolManager.ReturnWriter(eftWriter);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref Item value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = null;
            return;
        }

        Span<byte> span = default;
        reader.ReadUnmanagedSpan(ref span);

        if (span.Length == 0)
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
            var descriptor = eftReader.ReadEFTItemDescriptor();

            value = EFTItemSerializerClass.DeserializeItem(descriptor, Singleton<ItemFactoryClass>.Instance, []);
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