using System.Collections.Generic;
using System.Linq;
using MemoryPack;
using PacketWarden;
using Lambda.Core.Main.Gamemode;
using Lambda.Shared.Models;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct SessionManagerSyncPacket : IPacket
{
    public MatchState roundState;
    public BombState bombState;
    public int mvpId;
    public string mapName;

    public Dictionary<Faction, int> factionWins;
    public PlayerContextInfo?[] scores;
}

// Runs on MatchState.RoundEnd
public class SessionManagerSyncPacketWarden : LambdaPacketWarden<SessionManagerSyncPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public SessionManagerSyncPacket FormatPacket()
    {
        var scores = new PlayerContextInfo?[256];

        foreach (var (id, player) in H.Scoreboard.Entries)
        {
            scores[id] = player.Context;
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
        var session = H.Session;
        if (session == null) return;

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

        for (int id = 0; id < packet.scores.Length; id++)
        {
            var newInfo = packet.scores[id];

            if (!newInfo.HasValue)
                continue;

            var info = newInfo.Value;

            if (!H.Scoreboard.ContainsKey(id))
                H.Scoreboard[id] = new PlayerContext(id);

            var playerScore = H.Scoreboard[id]!;

            var oldFaction = playerScore.Faction;
            bool factionChanged = oldFaction != info.Faction;

            playerScore.Apply(info);

            if (!H.IsHeadless && factionChanged && id == H.MainPlayer.Id)
                EventBus.OnSelfFactionChanged?.Invoke(info.Faction);
        }
    }
}