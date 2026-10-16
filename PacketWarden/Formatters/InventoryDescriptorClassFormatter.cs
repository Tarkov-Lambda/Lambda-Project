using System;
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
        WriterPoolManager.WriteBlob(ref writer, eftWriter);
        WriterPoolManager.ReturnWriter(eftWriter);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref InventoryDescriptorClass value)
    {
        value = null;

        if (!reader.TryReadObjectHeader(out _)) return;
        if (!WriterPoolManager.TryReadBlob(ref reader, out ArraySegment<byte> blob)) return;

        try
        {
            using var eftReader = PacketToEFTReaderAbstractClass.Get(blob);
            value = eftReader.ReadEFTItemDescriptor();
        }
        catch (Exception) { }
    }
}
