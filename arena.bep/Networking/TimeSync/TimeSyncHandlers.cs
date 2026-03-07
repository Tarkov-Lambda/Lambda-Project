using Comfort.Common;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;

namespace ifp.arena.bep.networking.TimeSync
{
    // Client to server
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

        protected override bool ShouldBroadcastClientPacket(TimeSyncRequestPacket packet) => false;

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

        public override void WhenApproved(TimeSyncRequestPacket packet, NetPeer netPeer)
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

    // Server to client responding
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
