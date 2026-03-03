using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.networking;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.GameTypes
{
    public enum MatchState
    {
        // Just chilling type beat
        None,

        // Waiting for players to load
        Warmup,
        WarmupEnd,

        // Shared
        RoundPrepare,
        RoundAction,
        RoundEnd,

        // SND
        RoundPlanted,

        // Probably a good way to do these
        SideSwap,
        MatchEnd,
    }

    public class SessionInfo
    {
        public MatchState roundState = MatchState.None;
        public Dictionary<int, PlayerScore> scoreboard = new Dictionary<int, PlayerScore>();
        public Dictionary<Faction, int> factionWins = new Dictionary<Faction, int>();
        public BombState bombState = BombState.None;

        public GameModes currentGameMode = GameModes.SND;
        public string mapName = "gold_dust2";

        public Dictionary<MatchState, float> StateTimerConfig = new Dictionary<MatchState, float>
        {
            {MatchState.None, 0},
            {MatchState.Warmup, 120},
            {MatchState.WarmupEnd, 5},
            {MatchState.RoundPrepare, 10},
            {MatchState.RoundAction, 140},
            {MatchState.RoundEnd, 8},
            {MatchState.RoundPlanted, 45},
            {MatchState.SideSwap, 10},
            {MatchState.MatchEnd, 30}
        };

        public SessionInfo()
        {
            InitializeScoreBoard();
        }

        public void InitializeScoreBoard()
        {
            if (H.gameWorld == null || H.gameWorld.AllAlivePlayersList == null)
                return;

            factionWins[Faction.CT] = 0;
            factionWins[Faction.T] = 0;

            foreach (var p in H.GetAllPlayers())
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
            if (roundState == MatchState.RoundPrepare) return true;
            return false;
        }
    }

    public class PlayerScore
    {
        public PlayerScore(int id)
        {
            player = H.GetPlayer(id);
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