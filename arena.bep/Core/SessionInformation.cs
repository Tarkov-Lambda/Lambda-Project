using Comfort.Common;
using EFT;
using EFT.Hideout;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Audio;
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

        public int mvpId;

        public Dictionary<MatchState, float> StateTimerConfig = new Dictionary<MatchState, float>
        {
            {MatchState.None, 0},
            {MatchState.Warmup, 120},
            {MatchState.WarmupEnd, 5},
            {MatchState.RoundPrepare, 15},
            {MatchState.RoundAction, 140},
            {MatchState.RoundEnd, 8},
            {MatchState.RoundPlanted, 45},
            {MatchState.SideSwap, 10},
            {MatchState.MatchEnd, 15}
        };

        public SessionInfo()
        {
            InitializeScoreBoard();
        }

        public void InitializeScoreBoard()
        {
            if (H.GameWorld == null || H.GameWorld.AllAlivePlayersList == null)
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

        public void ResetRoundScopeFields()
        {
            if (H.GameWorld == null || H.GameWorld.AllAlivePlayersList == null)
                return;

            foreach (var p in H.GetAllPlayers())
            {
                if (scoreboard.ContainsKey(p.Id))
                {
                    scoreboard[p.Id].Reset();
                }
            }
        }

        // Locking out player shooting, moving, jumping during certain session states
        public bool IsControllerPartiallyLocked()
        {
            if (H.GameWorld is HideoutGameWorld) return false;
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

        public void Reset()
        {
            mvps = 0;
            kills = 0;
            headshots = 0;
            assists = 0;
            deaths = 0;
            money = 800;
            isAlive = true;
        }

        public void AwardMoney(int addedMoney)
        {
            money += addedMoney;
        }

        public Faction faction = Faction.None;
        public Player player;

        // Round scope
        public int mvps = 0;
        public int kills = 0;
        public int headshots = 0;
        public int assists = 0;
        public int deaths = 0;
        public int money = 800;
        public bool isAlive = true;
        public bool isReady = false;
        public string musicKit = "valve_cs2_01";
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

    public enum RoundWinReason
    {
        None,
        Objective,
        Elimination,
        Timeout
    }
}