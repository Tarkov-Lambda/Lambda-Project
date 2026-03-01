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
    public struct RestartPacket : INetSerializable
    {
        public string mapName;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(mapName);
        }

        public void Deserialize(NetDataReader reader)
        {
            mapName = reader.GetString();
        }
    }

    // Either when game mode has finished, or admin requests it. scoreboard is fresh.
    // NOTE: We are sending a SessionInfoPacket that updates info right before this (a little redundant but whatever)
    public class RestartPacketHandler : PacketHandler<RestartPacket>
    {
        public RestartPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send()
        {
            // Reset Scoreboard
            Singleton<BaseGameMode>.Instance.session.InitializeScoreBoard();

            Singleton<BaseGameMode>.Instance.session.mapName = Plugin.MapName.Value;
            
            // Send new info
            Singleton<SessionInfoPacketHandler>.Instance.Send();

            // Send packet signaling a restart
            var packet = new RestartPacket
            {
                mapName = Plugin.MapName.Value,
            };
            
            RequestSend(packet);
        }

        public override async void OnReceive(RestartPacket packet)
        {
            Plugin.Logger.LogInfo($"Host is sending {nameof(AssetLoadStatePacket)}");
            Plugin.Logger.LogInfo(packet.mapName);

            Singleton<FactionChangePacketHandler>.Instance.Send(Plugin.PrefferedFaction.Value);

            // Let the server drive the State Machine appropriately
            if (FikaBackendUtils.IsServer)
            {
                Singleton<BaseGameMode>.Instance.ChangeState(new StateWarmup());
            }

            await Singleton<AssetBundleHandler>.Instance.LoadMap(packet.mapName);

            Singleton<AssetLoadStatePacketHandler>.Instance.Send(true, "");


            EFT.Player player = Singleton<GameWorld>.Instance.MainPlayer;

            Teleporter.Teleport(player);
        }
    }
}
