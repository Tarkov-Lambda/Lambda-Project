using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking;
using MemoryPack;

namespace ifp.arena.bep.networking.TimeSync
{
    [MemoryPackable]
    public partial struct TimeSyncRequestPacket : INetSerializable
    {
        public double clientSendLocalSeconds;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);

        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<TimeSyncRequestPacket>(reader);
    }

    [MemoryPackable]
    public partial struct TimeSyncResponsePacket : INetSerializable
    {
        // NetPeer.Id of the intended recipient. Used so the server can broadcast but clients filter.
        public int targetPeerId;
        public double clientSendLocalSeconds;
        public double serverSendSeconds;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);

        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<TimeSyncResponsePacket>(reader);
    }
}
