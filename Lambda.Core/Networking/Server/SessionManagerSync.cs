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
    public Dictionary<int, PlayerScoreInfo> scores;
}

// Runs on MatchState.RoundEnd
public class SessionManagerSyncPacketWarden : LambdaPacketWarden<SessionManagerSyncPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public SessionManagerSyncPacket FormatPacket()
    {
        var packet = new SessionManagerSyncPacket
        {
            roundState = H.Session.matchState,
            bombState = H.Session.bombState,
            mvpId = H.Session.mvpId,
            mapName = H.Session.level,
            factionWins = H.Session.factionWins,
            scores = H.Scoreboard.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Score)
        };

        return packet;
    }

    public void Send()
    {
        var session = H.Session;
        if (session == null) return;

        DispatchPacket(FormatPacket());
    }

    public void SendToPeer(int peerId)
    {
        DispatchPacket(FormatPacket(), peerId);
    }

    protected override void Apply(SessionManagerSyncPacket packet, int peerId)
    {
        if (H.IsServer) return;

        H.Session.matchState = packet.roundState;
        H.Session.bombState = packet.bombState;
        H.Session.mvpId = packet.mvpId;
        H.Session.level = packet.mapName;
        H.Session.factionWins = packet.factionWins;

        foreach (var syncScore in packet.scores)
        {
            var id = syncScore.Key;
            var newInfo = syncScore.Value;

            if (!H.Scoreboard.ContainsKey(id))
                H.Scoreboard[id] = new PlayerContext(id);

            var playerScore = H.Scoreboard[id];

            var oldFaction = playerScore.Faction;
            bool factionChanged = oldFaction != newInfo.Faction;

            playerScore.Apply(newInfo);

            if (!H.IsHeadless && factionChanged && id == H.MainPlayer.Id)
                EventBus.OnSelfFactionChanged?.Invoke(newInfo.Faction);
        }
    }
}