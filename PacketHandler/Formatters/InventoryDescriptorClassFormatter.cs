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

        var eftWriter = WriterPoolManager.GetWriter();
        eftWriter.WriteEFTItemDescriptor(value);

        byte[] itemBytes = eftWriter.ToArray();
        WriterPoolManager.ReturnWriter(eftWriter);

        var compressedSpan = NetworkUtils.CompressBytes(itemBytes);

        writer.WriteUnmanaged(itemBytes.Length);
        writer.WriteUnmanagedSpan(compressedSpan);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref InventoryDescriptorClass value)
    {
        int originalLength = reader.ReadUnmanaged<int>();
        byte[] compressed = reader.ReadUnmanagedArray<byte>();

        if (compressed == null || compressed.Length == 0)
        {
            value = null;
            return;
        }

        byte[] itemBytes = NetworkUtils.DecompressBytes(compressed, originalLength);

        using var eftReader = PacketToEFTReaderAbstractClass.Get(itemBytes);
        value = eftReader.ReadEFTItemDescriptor();
    }
}
