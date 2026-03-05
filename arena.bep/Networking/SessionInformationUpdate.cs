using Comfort.Common;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using System.Collections.Generic;
using System.Linq;

namespace ifp.arena.bep.networking
{
    public struct PlayerScoreSyncData
    {
        public int playerId;
        public int faction;
        public int mvps;
        public int kills;
        public int headshots;
        public int assists;
        public int deaths;
        public bool isAlive;
        public bool isReady;
        public string musicKit;
    }

    public struct SessionInfoPacket : INetSerializable
    {
        public MatchState roundState;
        public GameModes gameMode;
        public BombState bombState;
        public int mvpId;
        public string mapName;
        
        public Dictionary<int, int> factionWins;
        public PlayerScoreSyncData[] scores;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)roundState);
            writer.Put((int)gameMode);
            writer.Put((int)bombState);
            writer.Put(mvpId);
            writer.Put(mapName);

            // Serialize Faction Wins Dictionary
            int winsCount = factionWins?.Count ?? 0;
            writer.Put(winsCount);
            if (factionWins != null)
            {
                foreach (var kvp in factionWins)
                {
                    writer.Put(kvp.Key);   // Faction (int)
                    writer.Put(kvp.Value); // Wins (int)
                }
            }

            int length = scores?.Length ?? 0;
            writer.Put(length);

            for (int i = 0; i < length; i++)
            {
                writer.Put(scores[i].playerId);
                writer.Put(scores[i].faction);
                writer.Put(scores[i].mvps);
                writer.Put(scores[i].kills);
                writer.Put(scores[i].headshots);
                writer.Put(scores[i].assists);
                writer.Put(scores[i].deaths);
                writer.Put(scores[i].isAlive);
                writer.Put(scores[i].isReady);
                writer.Put(scores[i].musicKit ?? string.Empty);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            roundState = (MatchState)reader.GetInt();
            gameMode = (GameModes)reader.GetInt();
            bombState = (BombState)reader.GetInt();
            mvpId = reader.GetInt();
            mapName = reader.GetString();

            int winsCount = reader.GetInt();
            factionWins = new Dictionary<int, int>();
            for (int i = 0; i < winsCount; i++)
            {
                int factionKey = reader.GetInt();
                int winValue = reader.GetInt();
                factionWins[factionKey] = winValue;
            }

            int length = reader.GetInt();
            scores = new PlayerScoreSyncData[length];
            for (int i = 0; i < length; i++)
            {
                scores[i] = new PlayerScoreSyncData
                {
                    playerId = reader.GetInt(),
                    faction = reader.GetInt(),
                    mvps = reader.GetInt(),
                    kills = reader.GetInt(),
                    headshots = reader.GetInt(),
                    assists = reader.GetInt(),
                    deaths = reader.GetInt(),
                    isAlive = reader.GetBool(),
                    isReady = reader.GetBool(),
                    musicKit = reader.GetString()
                };
            }
        }
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
                roundState = session.roundState,
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
                    isAlive = kvp.Value.isAlive,
                    isReady = kvp.Value.isReady,
                    musicKit = kvp.Value.musicKit
                }).ToArray()
            };

            RequestSend(packet);
        }

        public override void OnReceive(SessionInfoPacket packet, NetPeer peer)
        {
            var session = H.Session;
            if (session == null) return;

            session.roundState = packet.roundState;
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

            foreach (var syncScore in packet.scores)
            {
                if (session.scoreboard.TryGetValue(syncScore.playerId, out var playerScore))
                {
                    playerScore.faction = (Faction)syncScore.faction;
                    playerScore.mvps = syncScore.mvps;
                    playerScore.kills = syncScore.kills;
                    playerScore.headshots = syncScore.headshots;
                    playerScore.assists = syncScore.assists;
                    playerScore.deaths = syncScore.deaths;
                    playerScore.isAlive = syncScore.isAlive;
                    playerScore.isReady = syncScore.isReady;
                    playerScore.musicKit = syncScore.musicKit;
                }
                else
                {
                    session.scoreboard[syncScore.playerId] = new PlayerScore(syncScore.playerId)
                    {
                        faction = (Faction)syncScore.faction,
                        mvps = syncScore.mvps,
                        kills = syncScore.kills,
                        headshots = syncScore.headshots,
                        assists = syncScore.assists,
                        deaths = syncScore.deaths,
                        isAlive = syncScore.isAlive,
                        isReady = syncScore.isReady,
                        musicKit = syncScore.musicKit
                    };
                }
            }
        }
    }
}