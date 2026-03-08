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
        Pause, // Can only be invoked when in RoundPrepare
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
            {MatchState.Warmup, 1},
            {MatchState.WarmupEnd, 1},
            {MatchState.Pause, 1},
            {MatchState.RoundPrepare, 15},
            {MatchState.RoundAction, 115},
            {MatchState.RoundEnd, 8},
            {MatchState.RoundPlanted, 5},
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

            foreach (var p in H.AllPlayers)
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

            foreach (var p in H.AllPlayers)
            {
                if (scoreboard.ContainsKey(p.Id))
                {
                    scoreboard[p.Id].Reset();
                }
            }
        }

        // Locking out the player from shooting/jumping/moving
        public bool IsControllerPartiallyLocked()
        {
            if (H.GameWorld is HideoutGameWorld) return false;
            return false;

            if (roundState == MatchState.RoundPrepare || roundState == MatchState.Pause) return true;
            if (!H.MainPlayerScore.isAlive) return true;

            return false;
        }
    }

    public class PlayerScore
    {
        public Faction faction = Faction.None;
        public Player player;

        // Round scope
        public int mvps = 0;
        public int kills = 0;
        public int headshots = 0;
        public int assists = 0;
        public int deaths = 0;
        public int money = 8000;
        public bool isAlive = true;

        public bool isReady = false;

        public string musicKit = "valve_cs2_01";

        public int ping = 0;
        public bool isAdmin = false;

        public PlayerScore(int id)
        {
            player = H.GetPlayer(id);
            if (FikaBackendUtils.IsServer && H.MainPlayer.Id == id)
            {
                isAdmin = true;
            }
        }

        public void Reset()
        {
            mvps = 0;
            kills = 0;
            headshots = 0;
            assists = 0;
            deaths = 0;
            isAlive = true;
        }

        public void AwardMoney(int addedMoney)
        {
            money += addedMoney;
        }
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