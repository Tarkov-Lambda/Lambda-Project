using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ifp.arena.bep.networking
{
    public struct MatchStateSyncPacket : INetSerializable
    {
        public MatchState roundState;
        public double phaseDurationSeconds;
        public double serverPhaseStartSeconds;

        public bool hasRoundActionEnd;
        public RoundActionPhaseEnd roundActionEnd;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)roundState);
            writer.Put(phaseDurationSeconds);
            writer.Put(serverPhaseStartSeconds);

            writer.Put(hasRoundActionEnd);
            if (hasRoundActionEnd)
            {
                writer.Put(roundActionEnd.mvpId);
                writer.Put((int)roundActionEnd.winner);
                writer.Put((int)roundActionEnd.roundWinReason);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            roundState = (MatchState)reader.GetInt();
            phaseDurationSeconds = reader.GetDouble();
            serverPhaseStartSeconds = reader.GetDouble();

            hasRoundActionEnd = reader.GetBool();
            if (hasRoundActionEnd)
            {
                roundActionEnd = new RoundActionPhaseEnd
                {
                    mvpId = reader.GetInt(),
                    winner = (Faction)reader.GetInt(),
                    roundWinReason = (RoundWinReason)reader.GetInt()
                };
            }
        }
    }

    public class MatchStateSyncPacketHandler : PacketHandler<MatchStateSyncPacket>
    {
        public MatchStateSyncPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send(MatchState roundState, double phaseDurationSeconds, RoundActionPhaseEnd? roundActionEnd)
        {
            var packet = new MatchStateSyncPacket
            {
                roundState = roundState,
                phaseDurationSeconds = phaseDurationSeconds,
                serverPhaseStartSeconds = NetworkTime.ServerNowSeconds,
                hasRoundActionEnd = roundActionEnd.HasValue,
                roundActionEnd = roundActionEnd.GetValueOrDefault()
            };
            RequestSend(packet);
        }

        public override void OnReceive(MatchStateSyncPacket packet, NetPeer peer)
        {
            H.Arena.TransitionToState(packet);
        }
    }
}
