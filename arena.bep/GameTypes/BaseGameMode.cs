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
    public class BaseGameMode : Singleton<BaseGameMode>, IDisposable
    {
        public float RoundTime;
        public SessionInfo sessionInfo;

        public void startSession(GameWorld gameWorld)
        {
            sessionInfo = new SessionInfo();
        }

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
            Patch_Gameworld_OnGameStarted.OnGameStarted -= startSession;

            Release(this);
        }

        public void OnRoundStart()
        {

        }

        public void OnRoundEnd()
        {
            Singleton<SessionInfoPacketHandler>.Instance.Send();
        }
    }

    public class SessionInfo
    {
        public Dictionary<int, PlayerScore> scoreboard = new Dictionary<int, PlayerScore>();
        public GameModes currentGameMode = GameModes.SND;
        public string mapName = "gold_dust2";

        public SessionInfo()
        {
            InitializeScoreBoard();
        }

        public void InitializeScoreBoard()
        {
            foreach (var p in Singleton<GameWorld>.Instance.AllAlivePlayersList.ToArray())
            {
                if (!scoreboard.ContainsKey(p.Id))
                {
                    scoreboard[p.Id] = new PlayerScore();
                }
            }
        }
    }

    public class PlayerScore
    {
        public Faction faction = Faction.None;
        public int kills = 0;
        public int assists = 0;
        public int deaths = 0;
    }

    public enum BombState
    {
        None,
        Planting,
        Planted,
        Defusing,
        Defused,
        Exploded
    }
}