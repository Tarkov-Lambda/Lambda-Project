using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MemoryPack;
using Cysharp.Threading.Tasks;
using EFT;
using PacketHandler;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PlayerScoreSyncData
{
    public string musicKit;
    public int playerId;
    public int faction;
    public int mvps;
    public int kills;
    public int headshots;
    public int assists;
    public int deaths;
    public int money;
    public bool isAlive;
    public PlayerReadinessState readyState;

    public int RoundDamage;
    public int roundKills;
    public int roundHeadshots;
}

[MemoryPackable]
public partial struct SessionInfoPacket : INetSerializable
{
    public MatchState roundState;
    public GameModes gameMode;
    public BombState bombState;
    public int mvpId;
    public string mapName;

    public Dictionary<int, int> factionWins;
    public PlayerScoreSyncData[] scores;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<SessionInfoPacket>(reader);
}

// This only runs explicitly, not on interval
public class SessionInfoPacketHandler : PacketHandler<SessionInfoPacket>
{
    private CancellationTokenSource _cts = new();

    public SessionInfoPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.Dispose();
    }

    public SessionInfoPacket FormatPacket()
    {
        // Convert Faction Enum Dictionary to Int Dictionary for the packet
        var syncFactionWins = H.Session.factionWins.ToDictionary(k => (int)k.Key, v => v.Value);

        var packet = new SessionInfoPacket
        {
            roundState = H.Session.matchState,
            gameMode = H.Session.currentGameMode,
            bombState = H.Session.bombState,
            mvpId = H.Session.mvpId,
            mapName = H.Session.mapName,
            factionWins = syncFactionWins,

            // Loop through KeyValuePairs to create array
            scores = H.Session.scoreboard.Select(kvp => new PlayerScoreSyncData
            {
                playerId = kvp.Key,
                faction = (int)kvp.Value.Faction,
                mvps = kvp.Value.Mvps,
                kills = kvp.Value.Kills,
                headshots = kvp.Value.Headshots,
                assists = kvp.Value.Assists,
                deaths = kvp.Value.Deaths,
                money = kvp.Value.Money,
                isAlive = kvp.Value.IsAlive,
                readyState = kvp.Value.readyState,

                RoundDamage = kvp.Value.RoundDamage,
                roundKills = kvp.Value.RoundKills,
                roundHeadshots = kvp.Value.RoundHeadshots
            }).ToArray()
        };

        return packet;
    }

    public void Send()
    {
        var session = H.Session;
        if (session == null) return;

        DispatchPacket(FormatPacket());
    }

    public async void SendToPeer(NetPeer peer)
    {
        DispatchPacketToPeer(FormatPacket(), peer);
    }

    protected override void WhenApproved(SessionInfoPacket packet, NetPeer peer)
    {
        if (H.IsServer) return;

        H.Session.matchState = packet.roundState;
        H.Session.currentGameMode = packet.gameMode;
        H.Session.bombState = packet.bombState;
        H.Session.mvpId = packet.mvpId;
        H.Session.mapName = packet.mapName;

        if (packet.factionWins != null)
        {
            H.Session.factionWins.Clear();
            foreach (var kvp in packet.factionWins)
            {
                H.Session.factionWins[(Faction)kvp.Key] = kvp.Value;
            }
        }

        foreach (PlayerScoreSyncData syncScore in packet.scores)
        {
            if (!H.Session.scoreboard.ContainsKey(syncScore.playerId))
                H.Session.scoreboard[syncScore.playerId] = new PlayerScore(syncScore.playerId);
            H.Session.scoreboard[syncScore.playerId].Sync(syncScore);
        }
    }
}