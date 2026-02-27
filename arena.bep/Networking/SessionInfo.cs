using Comfort.Common;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using System.Linq;

namespace ifp.arena.bep.Networking
{
    public struct PlayerScoreSyncData
    {
        public int playerId;
        public int faction;
        public int kills;
        public int assists;
        public int deaths;
    }

    public struct SessionInfoPacket : INetSerializable
    {
        public GameModes gameMode;
        public PlayerScoreSyncData[] scores;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)gameMode);

            int length = scores?.Length ?? 0;
            writer.Put(length);

            for (int i = 0; i < length; i++)
            {
                writer.Put(scores[i].playerId);
                writer.Put(scores[i].faction);
                writer.Put(scores[i].kills);
                writer.Put(scores[i].assists);
                writer.Put(scores[i].deaths);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            gameMode = (GameModes)reader.GetInt();
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
                    deaths = reader.GetInt()
                };
            }
        }
    }

    public class SessionInfoPacketHandler : PacketHandler<SessionInfoPacket>
    {
        public void Send()
        {
            var session = Singleton<BaseGameMode>.Instance?.sessionInfo;
            if (session == null) return;

            var packet = new SessionInfoPacket
            {
                gameMode = session.currentGameMode,
                // Loop through KeyValuePairs instead of objects
                scores = session.scoreboard.Select(kvp => new PlayerScoreSyncData
                {
                    playerId = kvp.Key,
                    faction = (int)kvp.Value.faction,
                    kills = kvp.Value.kills,
                    assists = kvp.Value.assists,
                    deaths = kvp.Value.deaths
                }).ToArray()
            };

            OnSend(packet);
        }

        public override void OnReceive(SessionInfoPacket packet)
        {
            var session = Singleton<BaseGameMode>.Instance?.sessionInfo;
            if (session == null) return;

            session.currentGameMode = packet.gameMode;

            foreach (var syncScore in packet.scores)
            {
                // TryGetValue operates at an O(1) complexity unlike the O(N) LINQ FirstOrDefault
                if (session.scoreboard.TryGetValue(syncScore.playerId, out var playerScore))
                {
                    playerScore.faction = (Faction)syncScore.faction;
                    playerScore.kills = syncScore.kills;
                    playerScore.assists = syncScore.assists;
                    playerScore.deaths = syncScore.deaths;
                }
                else
                {
                    // Failsafe: if the client receives a score for a player not in their dictionary, add them.
                    session.scoreboard[syncScore.playerId] = new PlayerScore
                    {
                        faction = (Faction)syncScore.faction,
                        kills = syncScore.kills,
                        assists = syncScore.assists,
                        deaths = syncScore.deaths
                    };
                }
            }
        }
    }
}