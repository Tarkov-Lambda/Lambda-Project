using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
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
    public struct RoundStateSyncPacket : INetSerializable
    {
        public RoundState roundState;
        public double phaseDurationSeconds;
        public double serverPhaseStartSeconds;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)roundState);
            writer.Put(phaseDurationSeconds);
            writer.Put(serverPhaseStartSeconds);
        }

        public void Deserialize(NetDataReader reader)
        {
            roundState = (RoundState)reader.GetInt();
            phaseDurationSeconds = reader.GetDouble();
            serverPhaseStartSeconds = reader.GetDouble();
        }
    }

    public class RoundStateSyncPacketHandler : PacketHandler<RoundStateSyncPacket>
    {
        public RoundStateSyncPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send(RoundState roundState, double phaseDurationSeconds)
        {
            // serverPhaseStartSeconds is "now" on the server. Clients derive remaining time from it.
            double serverNow = NetworkTime.ServerNowSeconds;

            var packet = new RoundStateSyncPacket
            {
                roundState = roundState,
                phaseDurationSeconds = phaseDurationSeconds,
                serverPhaseStartSeconds = serverNow
            };

            RequestSend(packet);
        }

        public override void OnReceive(RoundStateSyncPacket packet, NetPeer peer)
        {
            NotificationManagerClass.DisplayMessageNotification($"{packet.roundState} {packet.phaseDurationSeconds} {packet.serverPhaseStartSeconds}");
            Singleton<BaseGameMode>.Instance.ApplyReplicatedRoundState(packet.roundState, packet.phaseDurationSeconds, packet.serverPhaseStartSeconds);
        }
    }
}
