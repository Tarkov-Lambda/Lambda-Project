using Fika.Core.Networking.LiteNetLib.Utils;

namespace ifp.arena.bep.networking.TimeSync
{
    public struct TimeSyncRequestPacket : INetSerializable
    {
        public double clientSendLocalSeconds;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(clientSendLocalSeconds);
        }

        public void Deserialize(NetDataReader reader)
        {
            clientSendLocalSeconds = reader.GetDouble();
        }
    }

    public struct TimeSyncResponsePacket : INetSerializable
    {
        // NetPeer.Id of the intended recipient. Used so the server can broadcast but clients filter.
        public int targetPeerId;
        public double clientSendLocalSeconds;
        public double serverSendSeconds;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(targetPeerId);
            writer.Put(clientSendLocalSeconds);
            writer.Put(serverSendSeconds);
        }

        public void Deserialize(NetDataReader reader)
        {
            targetPeerId = reader.GetInt();
            clientSendLocalSeconds = reader.GetDouble();
            serverSendSeconds = reader.GetDouble();
        }
    }
}
