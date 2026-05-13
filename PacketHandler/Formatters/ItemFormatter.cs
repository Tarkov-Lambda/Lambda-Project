using Comfort.Common;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
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
        
        writer.WriteObjectHeader(1);
        var eftWriter = WriterPoolManager.GetWriter();
        var descriptor = EFTItemSerializerClass.SerializeItem(value, FikaGlobals.SearchControllerSerializer);

        eftWriter.WriteEFTItemDescriptor(descriptor);
        byte[] itemBytes = eftWriter.ToArray();
        WriterPoolManager.ReturnWriter(eftWriter);

        writer.WriteUnmanagedSpan(itemBytes);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref Item value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = null;
            return;
        }

        byte[] itemBytes = reader.ReadUnmanagedArray<byte>();

        using var eftReader = PacketToEFTReaderAbstractClass.Get(itemBytes);
        var descriptor = eftReader.ReadEFTItemDescriptor();

        value = EFTItemSerializerClass.DeserializeItem(descriptor, Singleton<ItemFactoryClass>.Instance, []);
    }
}
