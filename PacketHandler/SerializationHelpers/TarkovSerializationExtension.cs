using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Pooling;
using MemoryPack;

namespace ifp.arena.bep.networking;

public static class TarkovSerializationExtension
{
    public static void PutPlayer(this NetDataWriter writer, Player player)
    {
        writer.Put(player.Id);
    }

    public static Player GetPlayer(this NetDataReader reader)
    {
        var playerId = reader.GetInt();
        return H.GetPlayer(playerId);
    }

    public static void Put(this NetDataWriter writer, ItemAddress itemAddress)
    {
        if (itemAddress == null)
        {
            writer.Put(false);
            return;
        }

        writer.Put(true);
        GClass1950 descriptor = itemAddress.ToDescriptor();
        EFTWriterClass eftWriter = WriterPoolManager.GetWriter();
        eftWriter.WritePolymorph(descriptor);
        byte[] _addressDescriptor = eftWriter.ToArray();
        WriterPoolManager.ReturnWriter(eftWriter);
        writer.PutByteArray(_addressDescriptor);
    }

    public static ItemAddress GetItemAddress(this NetDataReader reader, Player player)
    {
        if (!reader.GetBool())
        {
            return null;
        }

        byte[] _addressDescriptor = reader.GetByteArray();
        using var eftReader = PacketToEFTReaderAbstractClass.Get(_addressDescriptor);
        var descriptor = eftReader.ReadPolymorph<GClass1950>();
        return player.InventoryController.ToItemAddress(descriptor);
    }

    public static void PutItemCompressed(this NetDataWriter writer, Item item)
    {
        var eftWriter = WriterPoolManager.GetWriter();
        var descriptor = EFTItemSerializerClass.SerializeItem(item, FikaGlobals.SearchControllerSerializer);
        eftWriter.WriteEFTItemDescriptor(descriptor);
        writer.CompressAndPutByteArray(eftWriter.ToArray());
        WriterPoolManager.ReturnWriter(eftWriter);
    }

    public static Item GetItemCompressed(this NetDataReader reader)
    {
        var bytes = reader.DecompressAndGetByteArray();
        using var eftReader = PacketToEFTReaderAbstractClass.Get(bytes);
        return EFTItemSerializerClass.DeserializeItem(eftReader.ReadEFTItemDescriptor(), Singleton<ItemFactoryClass>.Instance, []);
    }
}


public class PlayerFormatter : MemoryPackFormatter<Player>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref Player value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(1);
        writer.WriteUnmanaged(value.Id);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref Player value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = null;
            return;
        }

        int id = reader.ReadUnmanaged<int>();
        value = H.GetPlayer(id);
    }
}

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

// TODO: Make this work for all IItemOwner and not just InventoryController subtype
public class ItemAddressFormatter : MemoryPackFormatter<ItemAddress>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref ItemAddress value)
    {
        if (value == null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        var playerProfileId = (value.GetOwner() as InventoryController)?.Profile?.ProfileId;

        if (playerProfileId == null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        Player addressPlayerOwner = null;
        foreach (var player in H.AllPlayers)
        {
            if (player.ProfileId == playerProfileId)
            {
                addressPlayerOwner = player;
                break;
            }
        }

        if (addressPlayerOwner == null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteUnmanaged(addressPlayerOwner.Id);

        var descriptor = value.ToDescriptor();
        var eftWriter = WriterPoolManager.GetWriter();

        eftWriter.WritePolymorph(descriptor);
        byte[] addressBytes = eftWriter.ToArray();

        WriterPoolManager.ReturnWriter(eftWriter);

        writer.WriteUnmanaged(addressBytes.Length);
        writer.WriteUnmanagedSpan((ReadOnlySpan<byte>)addressBytes);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref ItemAddress value)
    {
        int playerId = reader.ReadUnmanaged<int>();
        Player player = H.GetPlayer(playerId);

        int length = reader.ReadUnmanaged<int>();
        byte[] addressBytes = reader.ReadUnmanagedArray<byte>();

        if (addressBytes == null || addressBytes.Length == 0 || player == null)
        {
            value = null;
            return;
        }

        using var eftReader = PacketToEFTReaderAbstractClass.Get(addressBytes);

        var descriptor = eftReader.ReadPolymorph<GClass1950>();

        value = player.InventoryController.ToItemAddress(descriptor);
    }
}