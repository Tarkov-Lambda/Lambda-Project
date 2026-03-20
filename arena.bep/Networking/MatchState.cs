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
    public partial struct MatchStateSyncPacket : INetSerializable
    {
        public MatchState matchState;
        public double serverPhaseStartSeconds;
        public RoundActionPhaseEnd? roundActionEnd;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<MatchStateSyncPacket>(reader);
    }

    public class MatchStateSyncPacketHandler : PacketHandler<MatchStateSyncPacket>
    {
        public MatchStateSyncPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send(MatchState roundState, double phaseDurationSeconds, RoundActionPhaseEnd? roundActionEnd)
        {
            var packet = new MatchStateSyncPacket
            {
                matchState = roundState,
                serverPhaseStartSeconds = NetworkTime.ServerNowSeconds,
                roundActionEnd = roundActionEnd
            };
            RequestSend(packet);
        }

        protected override void WhenApproved(MatchStateSyncPacket packet, NetPeer peer)
        {
            H.Arena.TransitionToState(packet);
        }
    }
}
