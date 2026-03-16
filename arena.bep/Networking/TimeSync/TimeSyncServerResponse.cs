using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.Base;
using MemoryPack;

namespace ifp.arena.bep.networking.TimeSync
{

    [MemoryPackable]
    public partial struct TimeSyncResponsePacket : INetSerializable
    {
        public int targetPeerId;
        public double clientSendLocalSeconds;
        public double serverSendSeconds;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);

        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<TimeSyncResponsePacket>(reader);
    }

    public class TimeSyncResponsePacketHandler : PacketHandler<TimeSyncResponsePacket>
    {
        public TimeSyncResponsePacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public override void WhenApproved(TimeSyncResponsePacket packet, NetPeer peer)
        {
            if (FikaBackendUtils.IsServer)
                return;

            double clientReceiveLocal = NetworkTime.LocalNowSeconds;
            NetworkTime.ApplySample(packet.clientSendLocalSeconds, clientReceiveLocal, packet.serverSendSeconds);
        }
    }
}
