using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking
{
    [MemoryPackable]
    public partial struct PausePacket : INetSerializable
    {
        public int id;
        public double serverPhaseStartSeconds;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<PausePacket>(reader);
    }

    public class PausePacketHandler : PacketHandler<PausePacket>
    {
        public PausePacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.Both) { }

        public void Send()
        {
            var packet = new PausePacket
            {
                id = H.MainPlayer.Id,
            };

            if (FikaBackendUtils.IsServer)
            {
                packet.serverPhaseStartSeconds = NetworkTime.ServerNowSeconds;
            }

            RequestSend(packet);
        }

        protected override bool ServerValidation(ref PausePacket packet, NetPeer netPeer)
        {
            packet.serverPhaseStartSeconds = NetworkTime.ServerNowSeconds;
            if (H.Session.matchState == MatchState.RoundPrepare)
            {
                return true;
            } else return false;
        }

        protected override void WhenApproved(PausePacket packet, NetPeer peer)
        {
            MatchStateSyncPacket matchStateSyncPacket = new MatchStateSyncPacket
            {
                matchState = MatchState.Pause,
                serverPhaseStartSeconds = packet.serverPhaseStartSeconds,
            };
            H.Arena.TransitionToState(matchStateSyncPacket);
        }
    }
}
