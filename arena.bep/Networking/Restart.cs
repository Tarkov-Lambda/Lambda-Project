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
            if (H.Session == null) return;

            H.Session.scoreboard.Clear();
            H.Session.factionWins.Clear();
            H.Session.roundState = MatchState.None;
            H.Session.mapName = Plugin.MapName.Value;
            H.Session.currentGameMode = Plugin.GameMode.Value;
            H.Session.InitializeScoreBoard();

            Singleton<SessionInfoPacketHandler>.Instance.Send();

            var packet = new RestartPacket
            {
                mapName = Plugin.MapName.Value,
            };

            RequestSend(packet);
        }

        public override async void WhenApproved(RestartPacket packet, NetPeer peer)
        {
            _ = PlayerUtils.CloseEyes(false, false);
            await Task.Delay(500);
            Player player = H.GetMainPlayer();
            if (player != null)
            {
                Singleton<FactionChangePacketHandler>.Instance.Send(Plugin.PrefferedFaction.Value);
            }

            await Singleton<AssetBundleHandler>.Instance.LoadMap(packet.mapName);

            if (FikaBackendUtils.IsServer)
            {
                H.Arena.ChangeState(MatchState.Warmup);
            }

            Singleton<AssetLoadStatePacketHandler>.Instance.Send(true, "");

            switch (H.Session.currentGameMode)
            {
                case GameModes.FFA:
                    H.Arena.ActiveRules = new FFAModeRules();
                    break;
                case GameModes.SND:
                    H.Arena.ActiveRules = new SnDModeRules();
                    break;
            }

            await Task.Delay(25);

            if (player != null)
            {
                Teleporter.Teleport(player);
            }
            await Task.Delay(25);

            PlayerUtils.OpenEyes();
        }
    }
}
