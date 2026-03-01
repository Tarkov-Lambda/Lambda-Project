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
    public struct RoundStatePacket : INetSerializable
    {
        public RoundState roundState;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)roundState);
        }

        public void Deserialize(NetDataReader reader)
        {
            roundState = (RoundState)reader.GetInt();
        }
    }

    public class RoundStatePacketHandler : PacketHandler<RoundStatePacket>
    {
        public RoundStatePacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send(RoundState roundState)
        {
            var session = Singleton<BaseGameMode>.Instance?.session;
            if (session == null) return;

            var packet = new RoundStatePacket
            {
                roundState = roundState,
            };

            RequestSend(packet);
        }

        public override void OnReceive(RoundStatePacket packet)
        {
            var session = Singleton<BaseGameMode>.Instance?.session;
            if (session == null) return;

            session.roundState = packet.roundState;
            if (packet.roundState == RoundState.Prepare)
            {
                EFT.Player player = Singleton<GameWorld>.Instance.MainPlayer;
                Teleporter.Teleport(player);
                pActiveHealthController_Kill.FixMe(player.ActiveHealthController);
            }
        }
    }
}
