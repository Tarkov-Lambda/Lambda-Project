using Comfort.Common;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;

namespace ifp.arena.bep.networking
{
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
        public bool isReady;

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

        public void Send()
        {
            var session = H.Session;
            if (session == null) return;

            // Convert Faction Enum Dictionary to Int Dictionary for the packet
            var syncFactionWins = session.factionWins.ToDictionary(k => (int)k.Key, v => v.Value);

            var packet = new SessionInfoPacket
            {
                roundState = session.matchState,
                gameMode = session.currentGameMode,
                bombState = session.bombState,
                mvpId = session.mvpId,
                mapName = session.mapName,
                factionWins = syncFactionWins,

                // Loop through KeyValuePairs to create array
                scores = session.scoreboard.Select(kvp => new PlayerScoreSyncData
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
                    isReady = kvp.Value.isMapReady,

                    s_roundDamage = kvp.Value.s_roundDamage,
                    roundKills = kvp.Value.roundKills,
                    roundHeadshots = kvp.Value.roundHeadshots
                }).ToArray()
            };

            RequestSend(packet);
        }

        protected override void WhenApproved(SessionInfoPacket packet, NetPeer peer)
        {
            var session = H.Session;
            if (session == null) return;

            session.matchState = packet.roundState;
            session.currentGameMode = packet.gameMode;
            session.bombState = packet.bombState;
            session.mvpId = packet.mvpId;
            session.mapName = packet.mapName;

            if (packet.factionWins != null)
            {
                session.factionWins.Clear();
                foreach (var kvp in packet.factionWins)
                {
                    session.factionWins[(Faction)kvp.Key] = kvp.Value;
                }
            }

            foreach (PlayerScoreSyncData syncScore in packet.scores)
            {
                if (!session.scoreboard.ContainsKey(syncScore.playerId))
                    session.scoreboard[syncScore.playerId] = new PlayerScore(syncScore.playerId);
                session.scoreboard[syncScore.playerId].Sync(syncScore);
            }
        }
    }
}