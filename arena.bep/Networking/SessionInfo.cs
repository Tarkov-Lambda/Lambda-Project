using Comfort.Common;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Networking.Base;
using ifp.arena.shared;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ifp.arena.bep.Networking
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

    public struct RestartPacket : INetSerializable
    {
        public string mapName;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(mapName);
        }

        public void Deserialize(NetDataReader reader)
        {
            mapName = reader.GetString();
        }
    }

    public struct RoundStatePacket : INetSerializable
    {
        public RoundState roundState;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)roundState);
        }

        public void Deserialize(NetDataReader reader)
        {
            roundState = (RoundState)reader.GetInt();
        }
    }

    // event driven, mostly used during round end to sync
    public class SessionInfoPacketHandler : PacketHandler<SessionInfoPacket>
    {
        public SessionInfoPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send()
        {
            var session = Singleton<BaseGameMode>.Instance?.session;
            if (session == null) return;

            var packet = new SessionInfoPacket
            {
                roundState = session.roundState,
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

            RequestSend(packet);
        }

        public override void OnReceive(SessionInfoPacket packet)
        {
            var session = Singleton<BaseGameMode>.Instance?.session;
            if (session == null) return;

            session.roundState = packet.roundState;
            session.currentGameMode = packet.gameMode;
            session.mapName = packet.mapName;

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

            Plugin.Logger.LogInfo("SessionInfoPacketHandler");
        }
    }

    // Either when game mode has finished, or admin requests it. scoreboard is fresh.
    // NOTE: We are sending a SessionInfoPacket that updates info right before this (a little redundant but whatever)
    public class RestartPacketHandler : PacketHandler<RestartPacket>
    {
        public RestartPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send()
        {
            // Reset Scoreboard
            Singleton<BaseGameMode>.Instance.session.InitializeScoreBoard();

            // Send new info
            Singleton<SessionInfoPacketHandler>.Instance.Send();

            // Send packet signaling a restart
            var packet = new RestartPacket
            {
                mapName = Singleton<BaseGameMode>.Instance?.session.mapName,
            };
            
            RequestSend(packet);
        }

        public override async void OnReceive(RestartPacket packet)
        {
            Singleton<RoundStatePacketHandler>.Instance.Send(RoundState.Warmup);

            await Singleton<AssetBundleHandler>.Instance.LoadMap(packet.mapName);

            Singleton<AssetLoadStatePacketHandler>.Instance.Send(true, "");
        }
    }

    public class RoundStatePacketHandler : PacketHandler<RoundStatePacket>
    {
        public RoundStatePacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send(RoundState roundState)
        {
            var session = Singleton<BaseGameMode>.Instance?.session;
            if (session == null) return;

            var packet = new RoundStatePacket
            {
                roundState = roundState,

            };

            RequestSend(packet);
        }

        public override void OnReceive(RoundStatePacket packet)
        {
            var session = Singleton<BaseGameMode>.Instance?.session;
            if (session == null) return;

            session.roundState = packet.roundState;
            Plugin.Logger.LogInfo(Singleton<BaseGameMode>.Instance.session.roundState);
        }
    }

}
