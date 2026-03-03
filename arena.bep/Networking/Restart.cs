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
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ifp.arena.bep.networking
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
            if (H.session == null) return;

            H.session.scoreboard.Clear();
            H.session.factionWins.Clear();
            H.session.roundState = MatchState.None;
            H.session.mapName = Plugin.MapName.Value;
            H.session.currentGameMode = Plugin.GameMode.Value;
            H.session.InitializeScoreBoard();

            Singleton<SessionInfoPacketHandler>.Instance.Send();

            var packet = new RestartPacket
            {
                mapName = Plugin.MapName.Value,
            };

            RequestSend(packet);
        }

        public override async void OnReceive(RestartPacket packet, NetPeer peer)
        {
            Player player = H.GetMainPlayer();
            if (player != null)
            {
                Singleton<FactionChangePacketHandler>.Instance.Send(Plugin.PrefferedFaction.Value);
            }

            if (FikaBackendUtils.IsServer)
            {
                H.game.ChangeState(MatchState.RoundPrepare);
            }

            await Singleton<AssetBundleHandler>.Instance.LoadMap(packet.mapName);

            Singleton<AssetLoadStatePacketHandler>.Instance.Send(true, "");

            switch (H.session.currentGameMode)
            {
                case GameModes.FFA:
                    H.game.ActiveRules = new FFAModeRules();
                    break;
                case GameModes.SND:
                    H.game.ActiveRules = new SnDModeRules();
                    break;
            }
            if (player != null)
            {
                Teleporter.Teleport(player);
            }
        }
    }
}
