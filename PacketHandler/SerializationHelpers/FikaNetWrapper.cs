using Fika.Core.Networking.LiteNetLib.Utils;
using MemoryPack;

public struct FikaPacketWrapper<T> : INetSerializable where T : IPacket
{
    public T Payload;

    public readonly void Serialize(NetDataWriter writer)
    {
        byte[] bytes = MemoryPackSerializer.Serialize(Payload);
        writer.PutBytesWithLength(bytes);
    }

    public void Deserialize(NetDataReader reader)
    {
        byte[] bytes = reader.GetBytesWithLength();
        Payload = MemoryPackSerializer.Deserialize<T>(bytes);
    }
}