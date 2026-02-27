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
                scores = session.scoreboard.Select(s => new PlayerScoreSyncData
                {
                    playerId = s.p.Id,
                    faction = (int)s.faction,
                    kills = s.kills,
                    assists = s.assists,
                    deaths = s.deaths
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
                var playerScore = session.scoreboard.FirstOrDefault(p => p.p.Id == syncScore.playerId);

                if (playerScore != null)
                {
                    playerScore.faction = (Faction)syncScore.faction;
                    playerScore.kills = syncScore.kills;
                    playerScore.assists = syncScore.assists;
                    playerScore.deaths = syncScore.deaths;
                }
            }
        }
    }
}