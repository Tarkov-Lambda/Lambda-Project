using Comfort.Common;
using EFT;
using Fika.Core.Networking;
using ifp.arena.bep.Networking;
using ifp.arena.bep.Patches;
using ifp.arena.shared;
using System;
using System.Collections.Generic;

namespace ifp.arena.bep.GameTypes
{
    public class BaseGameMode : IDisposable
    {
        public float RoundTime;
        public SessionInfo sessionInfo;

        // Shit starts happening
        public void startSession(GameWorld gameWorld)
        {
            sessionInfo = new SessionInfo();
            foreach (var p in gameWorld.AllAlivePlayersList.ToArray())
            {
                PlayerScore newPlayer = new PlayerScore(p);
                sessionInfo.scoreboard.Add(newPlayer);
            }
        }

        // Shit stops happening
        public void endSession()
        {

        }

        public BaseGameMode()
        {
            // if statement for hot reloading
            if (Singleton<GameWorld>.Instance != null)
            {
                startSession(Singleton<GameWorld>.Instance);
            }
            Patch_Gameworld_OnGameStarted.OnGameStarted += startSession;
        }

        public void Dispose()
        {
            // if statement for hot reloading
            if (Singleton<IFikaNetworkManager>.Instance != null)
            {

            }
            Patch_Gameworld_OnGameStarted.OnGameStarted -= startSession;
        }

        public void ReportLocalDeath(int killerId, int victimId, int assistId = 1337)
        {
            var packet = new PlayerKilledPacket
            {
                KillerId = killerId,
                VictimId = victimId,
                AssistId = assistId
            };

            Singleton<PlayerKilledPacketHandler>.Instance.Send(packet);
        }
    }

    public class SessionInfo(GameModes gameMode = GameModes.SND)
    {
        public List<PlayerScore> scoreboard = new List<PlayerScore>();
        public GameModes currentGameMode = gameMode;
    }

    public class PlayerScore
    {
        public PlayerScore(EFT.Player player)
        {
            p = player;
        }

        public EFT.Player p;
        public Faction faction = Faction.None;
        public int kills = 0;
        public int assists = 0;
        public int deaths = 0;
    }
}
