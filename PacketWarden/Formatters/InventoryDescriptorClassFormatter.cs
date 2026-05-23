using System;
using System.Buffers;
using MemoryPack;

public class InventoryDescriptorClassFormatter : MemoryPackFormatter<InventoryDescriptorClass>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref InventoryDescriptorClass value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(1);

        var eftWriter = WriterPoolManager.GetWriter();
        eftWriter.WriteEFTItemDescriptor(value);

        var segment = eftWriter.ToArraySegment();
        writer.WriteUnmanagedSpan(segment.AsSpan());
        
        WriterPoolManager.ReturnWriter(eftWriter);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref InventoryDescriptorClass value)
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
            value = eftReader.ReadEFTItemDescriptor();
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