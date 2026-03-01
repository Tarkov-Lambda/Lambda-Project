using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Networking.Base;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ifp.arena.bep.Networking
{
    public struct RoundStateSyncPacket : INetSerializable
    {
        public RoundState roundState;
        public float phaseDurationSeconds;
        public float serverPhaseStartSeconds;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)roundState);
            writer.Put(phaseDurationSeconds);
            writer.Put(serverPhaseStartSeconds);
        }

        public void Deserialize(NetDataReader reader)
        {
            roundState = (RoundState)reader.GetInt();
            phaseDurationSeconds = reader.GetFloat();
            serverPhaseStartSeconds = reader.GetFloat();
        }
    }

    public class RoundStateSyncPacketHandler : PacketHandler<RoundStateSyncPacket>
    {
        public RoundStateSyncPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send(RoundState roundState, float phaseDurationSeconds)
        {
            // serverPhaseStartSeconds is "now" on the server. Clients derive remaining time from it.
            float serverNow = (float)Singleton<AbstractGame>.Instance.GameTimer.SessionTime.Value.TotalSeconds;

            var packet = new RoundStateSyncPacket
            {
                roundState = roundState,
                phaseDurationSeconds = phaseDurationSeconds,
                serverPhaseStartSeconds = serverNow
            };

            RequestSend(packet);
        }

        public override void OnReceive(RoundStateSyncPacket packet)
        {
            NotificationManagerClass.DisplayMessageNotification($"{packet.roundState} {packet.phaseDurationSeconds} {packet.serverPhaseStartSeconds}");
            Singleton<BaseGameMode>.Instance.ApplyReplicatedRoundState(packet.roundState, packet.phaseDurationSeconds, packet.serverPhaseStartSeconds);
        }
    }
}
