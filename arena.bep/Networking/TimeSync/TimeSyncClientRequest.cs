using Comfort.Common;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;
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

    public class TimeSyncRequestPacketHandler : PacketHandler<TimeSyncRequestPacket>
    {
        public TimeSyncRequestPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.Both) { }

        protected override RateLimitConfig ServerRateLimit => new(
            enabled: false,
            refillPerSecond: 5,
            burst: 10,
            costPerPacket: 1,
            action: RateLimitAction.Drop,
            stateTtlSeconds: 30,
            rejectCooldownSeconds: 0.5);

        protected override bool ShouldBroadcastPacket(TimeSyncRequestPacket packet) => false;

        public void Send()
        {
            if (FikaBackendUtils.IsServer)
                return;

            var packet = new TimeSyncRequestPacket
            {
                clientSendLocalSeconds = NetworkTime.LocalNowSeconds
            };

            RequestSend(packet);
        }

        protected override void WhenApproved(TimeSyncRequestPacket packet, NetPeer netPeer)
        {
            if (FikaBackendUtils.IsClient)
                return;

            var response = new TimeSyncResponsePacket
            {
                targetPeerId = netPeer.Id,
                clientSendLocalSeconds = packet.clientSendLocalSeconds,
                serverSendSeconds = NetworkTime.LocalNowSeconds
            };

            H.FikaNet.SendDataToPeer(ref response, DeliveryMethod.ReliableOrdered, netPeer);
        }
    }

}
