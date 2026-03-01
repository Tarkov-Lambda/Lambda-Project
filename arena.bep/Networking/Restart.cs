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
            var gameMode = Singleton<BaseGameMode>.Instance;
            if (gameMode?.session == null) return;

            gameMode.session.scoreboard.Clear();
            gameMode.session.factionWins.Clear();
            gameMode.session.roundState = RoundState.None;
            gameMode.session.mapName = Plugin.MapName.Value;
            gameMode.session.InitializeScoreBoard();
            
            Singleton<SessionInfoPacketHandler>.Instance.Send();

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

            var mainPlayer = Singleton<GameWorld>.Instance?.MainPlayer;
            if (mainPlayer != null)
            {
                Singleton<FactionChangePacketHandler>.Instance.Send(Plugin.PrefferedFaction.Value);
            }

            if (FikaBackendUtils.IsServer)
            {
                Singleton<BaseGameMode>.Instance.ChangeState(new StateWarmup());
            }

            await Singleton<AssetBundleHandler>.Instance.LoadMap(packet.mapName);

            Singleton<AssetLoadStatePacketHandler>.Instance.Send(true, "");


            EFT.Player player = Singleton<GameWorld>.Instance.MainPlayer;

            if (player != null)
            {
                Teleporter.Teleport(player);
            }
        }
    }
}
