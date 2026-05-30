using System;
using System.Threading;
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

        var eftWriter = WriterPoolManager.GetWriter();
        var descriptor = EFTItemSerializerClass.SerializeItem(value, GClass2240.Instance);
        eftWriter.WriteEFTItemDescriptor(descriptor);
        writer.WriteUnmanagedArray(eftWriter.ToArray());
        WriterPoolManager.ReturnWriter(eftWriter);
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
        catch (Exception)
        {
            // PacketWardenUtils.Log(ex.Message);
            // PacketWardenUtils.Log(ex.StackTrace);
        }
    }
}
