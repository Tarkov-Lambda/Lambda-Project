using Comfort.Common;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using ifp.arena.bep.Networking.Base;

namespace ifp.arena.bep.Networking.TimeSync
{
    /// <summary>
    /// Client -> Server request. Server replies with server send timestamp.
    /// </summary>
    public class TimeSyncRequestPacketHandler : PacketHandler<TimeSyncRequestPacket>
    {
        public TimeSyncRequestPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.Both) { }

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

        public override void OnReceive(TimeSyncRequestPacket packet, NetPeer netPeer)
        {
            // Server receives request and responds only to the requesting peer.
            if (!FikaBackendUtils.IsServer)
                return;

            var response = new TimeSyncResponsePacket
            {
                targetPeerId = netPeer.Id,
                clientSendLocalSeconds = packet.clientSendLocalSeconds,
                serverSendSeconds = NetworkTime.LocalNowSeconds
            };

            Singleton<IFikaNetworkManager>.Instance.SendDataToPeer(ref response, DeliveryMethod.ReliableOrdered, netPeer);
        }
    }

    /// <summary>
    /// Server -> Client time sync response.
    /// </summary>
    public class TimeSyncResponsePacketHandler : PacketHandler<TimeSyncResponsePacket>
    {
        public TimeSyncResponsePacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public override void OnReceive(TimeSyncResponsePacket packet, NetPeer peer)
        {
            if (FikaBackendUtils.IsServer)
                return;

            double clientReceiveLocal = NetworkTime.LocalNowSeconds;
            NetworkTime.ApplySample(packet.clientSendLocalSeconds, clientReceiveLocal, packet.serverSendSeconds);
        }
    }
}
