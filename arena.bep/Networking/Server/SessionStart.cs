using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct SessionStartPacket : INetSerializable
{
    public string mapName;

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<SessionStartPacket>(reader);
}

// Either when game mode has finished, or admin requests it. scoreboard is fresh.
// NOTE: We are sending a SessionInfoPacket that updates info right before this (a little redundant but whatever)
public class SessionStartPacketHandler : PacketHandler<SessionStartPacket>
{
    public SessionStartPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

    private void PrepareForRestart()
    {
        H.Session.scoreboard.Clear();
        H.Session.factionWins.Clear();
        H.Session.matchState = MatchState.None;
        H.Session.mapName = Plugin.MapName.Value;
        H.Session.currentGameMode = Plugin.GameMode.Value;
        H.Session.InitializeScoreBoard();

        Singleton<SessionInfoPacketHandler>.Instance.Send();
    }

    public void Send()
    {
        if (!H.isInRaid()) return;

        PrepareForRestart();

        var packet = new SessionStartPacket { mapName = Plugin.MapName.Value };
        RequestSend(packet);
    }

    // We only send restart packets to specific player under the condition that they just spawned/reconnected
    // for that reason we don't execute PrepareForRestart() here; I am not pleased with the way I'm doing it
    public void SendToPlayer(Player player)
    {
        if (!H.isInRaid()) return;

        var packet = new SessionStartPacket { mapName = Plugin.MapName.Value };
        RequestSendToPlayer(packet, player.Id);
    }

    protected override async void WhenApproved(SessionStartPacket packet, NetPeer peer)
    {
        D.LogTransaction("Starting a match");
        Singleton<FactionChangePacketHandler>.Instance.Send(Plugin.PrefferedFaction.Value);

        H.Arena.ChangeState(MatchState.Warmup);

        await Singleton<MapAssetBundleHandler>.Instance.LoadMap(packet.mapName);

        // Report back to the server that the map is loaded
        Singleton<PlayerReadinessPacketHandler>.Instance.Send(true, 100);

        switch (H.Session.currentGameMode)
        {
            case GameModes.FFA:
                H.Arena.ActiveRules = new FFAModeRules();
                break;
            case GameModes.SND:
                H.Arena.ActiveRules = new SND_ModeRules();
                break;
        }

        PU.OpenEyes();
    }
}