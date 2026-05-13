using Fika.Core.Networking;
using Fika.Core.Networking.Pooling;
using MemoryPack;

public class InventoryDescriptorClassFormatter : MemoryPackFormatter<InventoryDescriptorClass>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref InventoryDescriptorClass value)
    {
        if (value == null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteObjectHeader(1);
        var eftWriter = WriterPoolManager.GetWriter();
        eftWriter.WriteEFTItemDescriptor(value);

        byte[] itemBytes = eftWriter.ToArray();
        WriterPoolManager.ReturnWriter(eftWriter);

        writer.WriteUnmanagedSpan(itemBytes);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref InventoryDescriptorClass value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = null;
            return;
        }

        byte[] itemBytes = reader.ReadUnmanagedArray<byte>();

        using var eftReader = PacketToEFTReaderAbstractClass.Get(itemBytes);
        value = eftReader.ReadEFTItemDescriptor();
    }
}
