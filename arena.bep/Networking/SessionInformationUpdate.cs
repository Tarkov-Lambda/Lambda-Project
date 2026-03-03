using Comfort.Common;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using System.Linq;

namespace ifp.arena.bep.networking
{
    public struct PlayerScoreSyncData
    {
        public int playerId;
        public int faction;
        public int kills;
        public int assists;
        public int deaths;
        public bool isAlive;
    }

    public struct SessionInfoPacket : INetSerializable
    {
        public RoundState roundState;
        public GameModes gameMode;
        public PlayerScoreSyncData[] scores;
        public string mapName;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)roundState);
            writer.Put((int)gameMode);
            writer.Put(mapName);

            int length = scores?.Length ?? 0;
            writer.Put(length);

            for (int i = 0; i < length; i++)
            {
                writer.Put(scores[i].playerId);
                writer.Put(scores[i].faction);
                writer.Put(scores[i].kills);
                writer.Put(scores[i].assists);
                writer.Put(scores[i].deaths);
                writer.Put(scores[i].isAlive);

            }
        }

        public void Deserialize(NetDataReader reader)
        {
            roundState = (RoundState)reader.GetInt();
            gameMode = (GameModes)reader.GetInt();
            mapName = reader.GetString();

            int length = reader.GetInt();

            scores = new PlayerScoreSyncData[length];
            for (int i = 0; i < length; i++)
            {
                scores[i] = new PlayerScoreSyncData
                {
                    playerId = reader.GetInt(),
                    faction = reader.GetInt(),
                    kills = reader.GetInt(),
                    assists = reader.GetInt(),
                    deaths = reader.GetInt(),
                    isAlive = reader.GetBool()
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
            var session = H.session;
            if (session == null) return;

            var packet = new SessionInfoPacket
            {
                roundState = session.roundState,
                gameMode = session.currentGameMode,
                mapName = session.mapName,
                // Loop through KeyValuePairs instead of objects
                scores = session.scoreboard.Select(kvp => new PlayerScoreSyncData
                {
                    playerId = kvp.Key,
                    faction = (int)kvp.Value.faction,
                    kills = kvp.Value.kills,
                    assists = kvp.Value.assists,
                    deaths = kvp.Value.deaths,
                    isAlive = kvp.Value.isAlive
                }).ToArray()
            };

            RequestSend(packet);
        }

        public override void OnReceive(SessionInfoPacket packet, NetPeer peer)
        {
            var session = H.session;
            if (session == null) return;

            session.roundState = packet.roundState;
            session.currentGameMode = packet.gameMode;
            session.mapName = packet.mapName;

            foreach (var syncScore in packet.scores)
            {
                if (session.scoreboard.TryGetValue(syncScore.playerId, out var playerScore))
                {
                    playerScore.faction = (Faction)syncScore.faction;
                    playerScore.kills = syncScore.kills;
                    playerScore.assists = syncScore.assists;
                    playerScore.deaths = syncScore.deaths;
                    playerScore.isAlive = syncScore.isAlive;
                }
                else
                {
                    session.scoreboard[syncScore.playerId] = new PlayerScore(syncScore.playerId)
                    {
                        faction = (Faction)syncScore.faction,
                        kills = syncScore.kills,
                        assists = syncScore.assists,
                        deaths = syncScore.deaths,
                        isAlive = syncScore.isAlive
                    };
                }
            }
        }
    }
}
