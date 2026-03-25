using EFT;
using Fika.Core.Main.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public string mapName = "";

        public int mvpId;

        public Dictionary<MatchState, float> StateTimerConfig = new Dictionary<MatchState, float>
        {
            {MatchState.None, 0},
            {MatchState.Warmup, 40},
            {MatchState.WarmupEnd, 1},
            {MatchState.Pause, 45},
            {MatchState.RoundPrepare, 15},
            {MatchState.RoundAction, 115},
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
                    scoreboard[p.Id].RoundReset();
                }
            }
        }

        // Locking out the player from shooting/jumping/moving
        public bool IsControllerPartiallyLocked()
        {
            if (H.GameWorld is HideoutGameWorld) return false;
            // return false;

            if (roundState == MatchState.RoundPrepare || roundState == MatchState.Pause) return true;
            if (!H.MainPlayerScore.isAlive && H.Session.mapName != "") return true;

            return false;
        }

        public List<Player> GetPlayersFromFaction(Faction faction)
        {
            if (!H.isInRaid())
                return new();

            return scoreboard.Values
                .Where(s => s.faction == faction && s.player != null)
                .Select(s => s.player)
                .ToList();
        }
    }

    public class PlayerScore
    {
        public readonly Player player;

        public Faction faction = Faction.None;

        // Round scope
        public int kills { get; private set; }
        public int headshots { get; private set; }
        public int assists { get; private set; }
        public int deaths { get; private set; }
        public int mvps { get; private set; }

        public bool isAlive { get; private set; }
        public int money { get; private set; } = 0;

        // meta gaming (previously known as facebook gaming)
        public string musicKit = "";

        public bool isMapReady;
        public int ping;
        public bool IsAdmin;

        public PlayerScore(int id)
        {
            player = H.GetPlayer(id);
            if (FikaBackendUtils.IsServer && H.MainPlayer.Id == id)
            {
                IsAdmin = true;
            }
        }

        public void AddFrag(bool isHeadshot)
        {
            kills++;
            if (isHeadshot)
                headshots++;
        }

        public void Kill()
        {
            deaths++;
            isAlive = false;
        }

        public void Spawn()
        {
            isAlive = true;
            EventBus.OnSelfRespawn?.Invoke();
        }

        public void RoundReset()
        {
            mvps = 0;
            kills = 0;
            headshots = 0;
            assists = 0;
            deaths = 0;
            isAlive = true;
        }

        public void Sync(PlayerScoreSyncData packet)
        {
            faction = (Faction)packet.faction;
            mvps = packet.mvps;
            kills = packet.kills;
            headshots = packet.headshots;
            assists = packet.assists;
            deaths = packet.deaths;
            money = packet.money;
            isAlive = packet.isAlive;
            isMapReady = packet.isReady;
            musicKit = packet.musicKit;
        }

        public void AddMoney(int amount)
        {
            money += amount;

            money = Math.Clamp(money, 0, EconomyConstants.MAX_MONEY);

            if (player == H.MainPlayer)
                EventBus.OnSelfMoneyAdded?.Invoke(amount);
        }

        public void SpendMoney(int amount)
        {
            money -= amount;
            if (money < 0)
                money = 0;
        }

        public void SetMoney(int newMoney)
        {
            money = newMoney;
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