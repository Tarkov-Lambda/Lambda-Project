using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Networking;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.GameTypes
{
    public enum RoundState
    {
        None,
        Warmup,
        WarmupEnd,
        Prepare,
        Action,
        Planted,
        End
    }

    public class SessionInfo
    {
        public RoundState roundState = RoundState.None;
        public Dictionary<int, PlayerScore> scoreboard = new Dictionary<int, PlayerScore>();
        public Dictionary<Faction, int> factionWins = new Dictionary<Faction, int>();
        public BombState bombState = BombState.None;

        public GameModes currentGameMode = GameModes.SND;
        public string mapName = "gold_dust2";

        public SessionInfo()
        {
            InitializeScoreBoard();
        }

        public void InitializeScoreBoard()
        {
            if (Singleton<GameWorld>.Instance == null || Singleton<GameWorld>.Instance.AllAlivePlayersList == null)
                return;

            foreach (var p in Singleton<GameWorld>.Instance.AllAlivePlayersList.ToArray())
            {
                if (!scoreboard.ContainsKey(p.Id))
                {
                    scoreboard[p.Id] = new PlayerScore(p.Id);
                }
            }
        }

        // Locking out player shooting, moving, jumping during certain session states
        public bool IsControllerPartiallyLocked()
        {
            if (roundState == RoundState.Prepare) return true;
            return false;
        }
    }

    public class PlayerScore
    {
        public PlayerScore(int id)
        {
            this.player = Singleton<GameWorld>.Instance.AllAlivePlayersList.FirstOrDefault(p => p.Id == id);
        }

        public Faction faction = Faction.None;
        public EFT.Player player;
        public int kills = 0;
        public int assists = 0;
        public int deaths = 0;
        public bool isAlive = true;
        public bool isReady = false;
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