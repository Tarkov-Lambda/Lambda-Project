using System;
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

        byte[] itemBytes = eftWriter.ToArray();
        
        WriterPoolManager.ReturnWriter(eftWriter);

        writer.WriteUnmanagedArray(itemBytes);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref Item value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = null;
            return;
        }

        byte[] itemBytes = reader.ReadUnmanagedArray<byte>();

        if (itemBytes == null || itemBytes.Length == 0)
        {
            value = null;
            return;
        }

        try
        {
            using var eftReader = PacketToEFTReaderAbstractClass.Get(itemBytes);
            var descriptor = eftReader.ReadEFTItemDescriptor();

            value = EFTItemSerializerClass.DeserializeItem(descriptor, Singleton<ItemFactoryClass>.Instance, []);
        }
        catch (Exception) { }
    }
}
