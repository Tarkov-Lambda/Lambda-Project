using Fika.Core.Networking.LiteNetLib.Utils;

public struct FikaPacketWrapper<T> : INetSerializable where T : IPacket
{
    public T Payload;

    public readonly void Serialize(NetDataWriter writer)
    {
        MemoryPackWrapper.Serialize(writer, Payload);
    }

    public void Deserialize(NetDataReader reader)
    {
        Payload = MemoryPackWrapper.Deserialize<T>(reader);
    }
}