using System;
using Comfort.Common;
using EFT.InventoryLogic;
using MemoryPack;

public class ItemFormatter : MemoryPackFormatter<Item>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer,
        scoped ref Item value
    )
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(1);

        var eftWriter = WriterPoolManager.GetWriter();
        eftWriter.WriteEFTItemDescriptor(
            EFTItemSerializerClass.SerializeItem(value, GClass2240.Instance)
        );
        WriterPoolManager.WriteBlob(ref writer, eftWriter);
        WriterPoolManager.ReturnWriter(eftWriter);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref Item value)
    {
        value = null;

        if (!reader.TryReadObjectHeader(out _))
            return;
        if (!WriterPoolManager.TryReadBlob(ref reader, out ArraySegment<byte> blob))
            return;

        try
        {
            using var eftReader = PacketToEFTReaderAbstractClass.Get(blob);
            value = EFTItemSerializerClass.DeserializeItem(
                eftReader.ReadEFTItemDescriptor(),
                Singleton<ItemFactoryClass>.Instance,
                []
            );
        }
        catch (Exception) { }
    }
}
