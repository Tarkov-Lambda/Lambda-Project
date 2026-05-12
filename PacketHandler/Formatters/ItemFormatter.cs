using Comfort.Common;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.Pooling;
using MemoryPack;

public class ItemFormatter : MemoryPackFormatter<Item>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref Item value)
    {
        if (value == null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        var eftWriter = WriterPoolManager.GetWriter();
        var descriptor = EFTItemSerializerClass.SerializeItem(value, FikaGlobals.SearchControllerSerializer);

        eftWriter.WriteEFTItemDescriptor(descriptor);
        byte[] itemBytes = eftWriter.ToArray();
        WriterPoolManager.ReturnWriter(eftWriter);

        var compressedSpan = NetworkUtils.CompressBytes(itemBytes);

        writer.WriteUnmanaged(itemBytes.Length);
        writer.WriteUnmanagedSpan(compressedSpan);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref Item value)
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
        var descriptor = eftReader.ReadEFTItemDescriptor();

        value = EFTItemSerializerClass.DeserializeItem(descriptor, Singleton<ItemFactoryClass>.Instance, []);
    }
}
