using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.Core.UI;
using ifp.arena.bep.GameTypes;
using PacketHandler;
using ifp.arena.shared;
using MemoryPack;
using ifp.arena.bep.networking.TimeSync;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct SessionStartPacket : INetSerializable
{
    public string mapName;
    public GameModes gameMode;


    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<SessionStartPacket>(reader);
}

// Either when game mode has finished, or admin requests it. scoreboard is fresh.
public class SessionStartPacketHandler : PacketHandler<SessionStartPacket>
{
    public SessionStartPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.Admin) { }

    private void PrepareForStart(SessionStartPacket packet)
    {
        foreach (var playerScore in H.Session.scoreboard.Values)
        {
            playerScore.SessionReset();
        }

        H.Session.factionWins.Clear();
        H.Session.matchState = MatchState.None;
        H.Session.mapName = packet.mapName;
        H.Session.currentGameMode = packet.gameMode;
        H.Session.InitializeScoreBoard();
    }

    public void Send()
    {
        if (!H.IsInRaid()) return;

        var packet = new SessionStartPacket
        {
            mapName = Plugin.MapName.Value,
            gameMode = Plugin.GameMode.Value
        };

        DispatchPacket(packet);
    }

    // if a player was not present at the start of this session, send them the sitrep
    public void SendToPeer(NetPeer peer)
    {
        if (!H.IsInRaid()) return;

        var packet = new SessionStartPacket
        {
            mapName = H.Session.mapName,
            gameMode = H.Session.currentGameMode
        };
        DispatchPacketToPeer(packet, peer);
    }

    protected override async void WhenApproved(SessionStartPacket packet, NetPeer peer)
    {
        PrepareForStart(packet);

        D.LogTransaction("Starting a match");

        switch (H.Session.currentGameMode)
        {
            case GameModes.FFA:
                H.Arena.ActiveRules = new FFAModeRules();
                break;
            case GameModes.SND:
                H.Arena.ActiveRules = new SND_ModeRules();
                break;
            case GameModes.AWP:
                H.Arena.ActiveRules = new AWP_ModeRules();
                break;
        }

        if (!H.IsClient)
        {
            Singleton<SessionManagerSyncPacketHandler>.Instance.Send();
            H.Arena.ChangeState(MatchState.Warmup);
        }

        if (!H.IsHeadless)
        {
            NetworkTime.Reset();
            Singleton<FactionChangePacketHandler>.Instance.Send(Plugin.PrefferedFaction.Value);

            await Singleton<MapAssetBundleHandler>.Instance.LoadMap(packet.mapName);

            // Report back to the server that the map is loaded
            Singleton<PlayerReadinessPacketHandler>.Instance.Send(PlayerReadinessState.Ready);

            PU.OpenEyes();
        }
    }
}