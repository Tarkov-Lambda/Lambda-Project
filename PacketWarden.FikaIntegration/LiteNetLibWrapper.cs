using System;
using System.Buffers;
using Fika.Core.Networking.LiteNetLib.Utils;
using MemoryPack;

// FNV-1a of the same name Fika hashes (stable across assembly versions).
// 32 bits instead of Fika's 16 keeps ~100 packet types well clear of collisions.
internal static class PacketId<T>
{
    public static readonly uint Value = Hash(typeof(T).ToString());

    private static uint Hash(string name)
    {
        uint hash = 2166136261;

        foreach (char c in name)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return hash;
    }
}

// The only type PacketWarden registers with Fika: [uint id][int length][MemoryPack payload].
// Deserialize always consumes exactly its own bytes and never throws, so a malformed payload
// can't take down the rest of Fika's packet batch.
internal struct LiteNetLibWrapper : INetSerializable
{
    [ThreadStatic]
    private static NetDataWriter _scratch;

    public uint Id;
    public ArraySegment<byte> Payload;

    public bool IsValid => Payload.Array != null;

    // The payload is only valid until the next Create
    public static LiteNetLibWrapper Create<T>(ref T packet) where T : IPacket
    {
        _scratch ??= new NetDataWriter();
        _scratch.Reset();

        MemoryPackSerializer.Serialize(NetDataBufferWriter.Wrap(_scratch), packet);

        return new LiteNetLibWrapper
        {
            Id = PacketId<T>.Value,
            Payload = new ArraySegment<byte>(_scratch.Data, 0, _scratch.Length)
        };
    }

    public readonly void Serialize(NetDataWriter writer)
    {
        writer.Put(Id);
        writer.Put(Payload.Count);
        writer.Put(new ReadOnlySpan<byte>(Payload.Array, Payload.Offset, Payload.Count));
    }

    public void Deserialize(NetDataReader reader)
    {
        Payload = default;

        if (!reader.TryGetUInt(out Id) || !reader.TryGetInt(out int length) || length < 0 || length > reader.AvailableBytes)
        {
            reader.SkipBytes(reader.AvailableBytes);
            return;
        }

        Payload = new ArraySegment<byte>(reader.RawData, reader.Position, length);
        reader.SkipBytes(length);
    }
}

internal sealed class NetDataBufferWriter : IBufferWriter<byte>
{
    [ThreadStatic]
    private static NetDataBufferWriter _shared;

    private NetDataWriter _writer;

    public static NetDataBufferWriter Wrap(NetDataWriter writer)
    {
        _shared ??= new NetDataBufferWriter();
        _shared._writer = writer;
        return _shared;
    }

    public void Advance(int count) => _writer.SetPosition(_writer.Length + count);

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        _writer.EnsureFit(Math.Max(sizeHint, 256));
        return _writer.Data.AsMemory(_writer.Length);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        _writer.EnsureFit(Math.Max(sizeHint, 256));
        return _writer.Data.AsSpan(_writer.Length);
    }
}