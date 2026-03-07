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
    public struct PausePacket : INetSerializable
    {
        public int id;
        public double serverPhaseStartSeconds;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(id);
            writer.Put(serverPhaseStartSeconds);
        }

        public void Deserialize(NetDataReader reader)
        {
            id = reader.GetInt();
            serverPhaseStartSeconds = reader.GetDouble();
        }
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

        public override bool ServerValidation(ref PausePacket packet, NetPeer netPeer)
        {
            packet.serverPhaseStartSeconds = NetworkTime.ServerNowSeconds;
            if (H.Session.roundState == MatchState.RoundPrepare)
            {
                return true;
            } else return false;
        }

        public override void WhenApproved(PausePacket packet, NetPeer peer)
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
