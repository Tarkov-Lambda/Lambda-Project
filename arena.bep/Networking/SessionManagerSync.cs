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
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.shared.Models;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct SessionManagerSyncPacket : INetSerializable
{
    public MatchState roundState;
    public GameModes gameMode;
    public BombState bombState;
    public int mvpId;
    public string mapName;

    public Dictionary<Faction, int> factionWins;
    public Dictionary<int, PlayerScoreInfo> scores;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<SessionManagerSyncPacket>(reader);
}

// This only runs explicitly, not on interval
public class SessionManagerSyncPacketHandler : PacketHandler<SessionManagerSyncPacket>
{
    private CancellationTokenSource _cts = new();

    public SessionManagerSyncPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.Dispose();
    }

    public SessionManagerSyncPacket FormatPacket()
    {
        var packet = new SessionManagerSyncPacket
        {
            roundState = H.Session.matchState,
            gameMode = H.Session.currentGameMode,
            bombState = H.Session.bombState,
            mvpId = H.Session.mvpId,
            mapName = H.Session.mapName,
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

    public async void SendToPeer(NetPeer peer)
    {
        DispatchPacketToPeer(FormatPacket(), peer);
    }

    protected override void WhenApproved(SessionManagerSyncPacket packet, NetPeer peer)
    {
        if (H.IsServer) return;

        H.Session.matchState = packet.roundState;
        H.Session.currentGameMode = packet.gameMode;
        H.Session.bombState = packet.bombState;
        H.Session.mvpId = packet.mvpId;
        H.Session.mapName = packet.mapName;
        H.Session.factionWins = packet.factionWins;

        foreach (var syncScore in packet.scores)
        {
            var id = syncScore.Key;
            var newInfo = syncScore.Value;

            if (!H.Scoreboard.ContainsKey(id))
                H.Scoreboard[id] = new PlayerScore(id);

            var playerScore = H.Scoreboard[id];

            var oldFaction = playerScore.Faction;
            bool factionChanged = oldFaction != newInfo.Faction;

            playerScore.Apply(newInfo);

            if (!H.IsHeadless && factionChanged && id == H.MainPlayer.Id)
                EventBus.OnSelfFactionChanged?.Invoke(newInfo.Faction);
        }
    }
}