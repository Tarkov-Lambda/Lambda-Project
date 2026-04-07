using Comfort.Common;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;
using System;
using Fika.Core.Main.Players;
using Cysharp.Threading.Tasks;
using Fika.Core.Main.Utils;
using EFT;

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

    public int s_roundDamage;
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

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<SessionInfoPacket>(reader);
}

// This only runs explicitly, not on interval
public class SessionInfoPacketHandler : PacketHandler<SessionInfoPacket>
{
    public SessionInfoPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

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
                faction = (int)kvp.Value.faction,
                mvps = kvp.Value.mvps,
                kills = kvp.Value.kills,
                headshots = kvp.Value.kills,
                assists = kvp.Value.assists,
                deaths = kvp.Value.deaths,
                money = kvp.Value.money,
                isAlive = kvp.Value.isAlive,
                readyState = kvp.Value.readyState,

                s_roundDamage = kvp.Value.s_roundDamage,
                roundKills = kvp.Value.roundKills,
                roundHeadshots = kvp.Value.roundHeadshots
            }).ToArray()
        };

        return packet;
    }

    public void Send()
    {
        var session = H.Session;
        if (session == null) return;

        RequestSend(FormatPacket());
    }

    public async void SendToPlayer(Player player)
    {
        var session = H.Session;
        if (session == null) return;

        await UniTask.WaitUntil(() => H.GetPlayerScore(player.Id).readyState <= PlayerReadinessState.Connected);
        RequestSendToPlayer(FormatPacket(), player.Id);
    }

    protected override void WhenApproved(SessionInfoPacket packet, NetPeer peer)
    {
        if (FikaBackendUtils.IsServer) return;

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