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
using MemoryPack;
using System.Threading.Tasks;

namespace ifp.arena.bep.networking
{
    [MemoryPackable]
    public partial struct RestartPacket : INetSerializable
    {
        public string mapName;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<RestartPacket>(reader);
    }

    // Either when game mode has finished, or admin requests it. scoreboard is fresh.
    // NOTE: We are sending a SessionInfoPacket that updates info right before this (a little redundant but whatever)
    public class RestartPacketHandler : PacketHandler<RestartPacket>
    {
        public RestartPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        private void PrepareForRestart()
        {
            H.Session.scoreboard.Clear();
            H.Session.factionWins.Clear();
            H.Session.roundState = MatchState.None;
            H.Session.mapName = Plugin.MapName.Value;
            H.Session.currentGameMode = Plugin.GameMode.Value;
            H.Session.InitializeScoreBoard();

            Singleton<SessionInfoPacketHandler>.Instance.Send();
        }

        public void Send()
        {
            if (H.Session == null) return;

            if (FikaBackendUtils.IsServer)
            {
                PrepareForRestart();
            }

            var packet = new RestartPacket
            {
                mapName = Plugin.MapName.Value,
            };

            RequestSend(packet);
        }

        protected override bool ServerValidation(ref RestartPacket packet, NetPeer netPeer)
        {
            PrepareForRestart();
            return base.ServerValidation(ref packet, netPeer);
        }

        protected override async void WhenApproved(RestartPacket packet, NetPeer peer)
        {
            Player player = H.GetMainPlayer();
            if (player != null)
            {
                Singleton<FactionChangePacketHandler>.Instance.Send(Plugin.PrefferedFaction.Value);
            }

            H.LogTransaction("Starting a match");
            if (FikaBackendUtils.IsServer)
            {
                H.Arena.ChangeState(MatchState.Warmup);
            }

            await Singleton<AssetBundleHandler>.Instance.LoadMap(packet.mapName);

            // Report back to the server that the map is loaded
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

            PU.OpenEyes();
        }
    }
}
