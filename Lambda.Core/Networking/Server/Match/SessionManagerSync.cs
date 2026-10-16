using System.Collections.Generic;
using MemoryPack;
using PacketWarden;
using Lambda.Core.Main.Gamemode;
using Lambda.Shared.Models;
using Lambda.Core.Main;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct PlayerContextEntry
{
    public int playerId;
    public PlayerContextInfo info;
}

[MemoryPackable]
public partial struct SessionManagerSyncPacket : IPacket
{
    public MatchState roundState;
    public BombState bombState;
    public int mvpId;
    public string mapName;

    public Dictionary<Faction, int> factionWins;
    public PlayerContextEntry[] scores;
}

// Runs on MatchState.RoundEnd
public class SessionManagerSyncPacketWarden : LambdaPacketWarden<SessionManagerSyncPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public SessionManagerSyncPacket FormatPacket()
    {
        List<PlayerContext> contexts = H.Scoreboard.Values;
        var scores = new PlayerContextEntry[contexts.Count];

        for (int i = 0; i < contexts.Count; i++)
        {
            scores[i] = new PlayerContextEntry
            {
                playerId = contexts[i].Id,
                info = contexts[i].Context
            };
        }

        return new SessionManagerSyncPacket
        {
            roundState = H.Session.matchState,
            bombState = H.Session.bombState,
            mvpId = H.Session.mvpId,
            mapName = H.Session.level,
            factionWins = H.Session.factionWins,
            scores = scores
        };
    }

    public void Send()
    {
        if (H.Session == null) return;

        var packet = FormatPacket();
        DispatchPacket(ref packet);
    }

    public void SendToPeer(int peerId)
    {
        var packet = FormatPacket();
        DispatchPacket(ref packet, peerId);
    }

    protected override void Apply(SessionManagerSyncPacket packet, int peerId)
    {
        if (H.IsServer) return;

        H.Session.matchState = packet.roundState;
        H.Session.bombState = packet.bombState;
        H.Session.mvpId = packet.mvpId;
        H.Session.level = packet.mapName;
        H.Session.factionWins = packet.factionWins;

        if (packet.scores == null) return;

        int mainPlayerId = H.IsHeadless ? -1 : H.MainPlayer.Id;

        foreach (PlayerContextEntry entry in packet.scores)
        {
            // players that already left only exist on the server
            if (H.GetPlayer(entry.playerId) == null) continue;

            PlayerContext playerScore = H.GetPlayerContext(entry.playerId);

            bool factionChanged = playerScore.Faction != entry.info.Faction;

            playerScore.Apply(entry.info);

            if (factionChanged && entry.playerId == mainPlayerId)
                EventBus.OnSelfFactionChanged?.Invoke(entry.info.Faction);
        }
    }
}