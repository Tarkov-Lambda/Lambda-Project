using EFT;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ifp.arena.bep.GameTypes;

public class SessionManager
{
    public MatchState matchState = MatchState.None;
    public Dictionary<int, PlayerScore> scoreboard = new Dictionary<int, PlayerScore>();
    public Dictionary<Faction, int> factionWins = new Dictionary<Faction, int>();
    public BombState bombState = BombState.None;

    public GameModes currentGameMode = GameModes.SND;
    public string mapName = "";

    public int mvpId;

    public Dictionary<MatchState, float> StateTimerConfig = new Dictionary<MatchState, float>
        {
            {MatchState.None, 0},
            {MatchState.Warmup, 120},
            {MatchState.WarmupEnd, 5},
            {MatchState.Pause, 45},
            {MatchState.RoundPrepare, 5},
            {MatchState.RoundAction, 115},
            {MatchState.RoundEnd, 8},
            {MatchState.RoundPlanted, 45},
            {MatchState.SideSwap, 10},
            {MatchState.MatchEnd, 15}
        };

    public SessionManager()
    {
        InitializeScoreBoard();
    }

    public void InitializeScoreBoard()
    {
        factionWins[Faction.CT] = 0;
        factionWins[Faction.T] = 0;

        try
        {
            foreach (var p in H.AllPlayers)
            {
                if (p == null) continue;
                if (!scoreboard.ContainsKey(p.Id))
                {
                    scoreboard[p.Id] = new PlayerScore(p.Id);
                }
            }
        }
        catch (Exception ex)
        {
            D.Log(ex.StackTrace);
        }
    }

    public void ResetSessionScopeFields()
    {
        if (H.GameWorld == null || H.GameWorld.AllAlivePlayersList == null)
            return;

        foreach (var p in H.AllPlayers)
        {
            if (scoreboard.ContainsKey(p.Id))
            {
                scoreboard[p.Id].SessionReset();
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
        if (H.IsHeadless) return false;
        if (H.GameWorld is HideoutGameWorld) return false;

        if (matchState == MatchState.WarmupEnd ||
            matchState == MatchState.RoundPrepare ||
            matchState == MatchState.Pause) return true;

        if (!H.MainPlayerScore.IsAlive && H.Session.mapName != "") return true;

        return false;
    }

    public List<Player> GetPlayersFromFaction(Faction faction)
    {
        if (!H.IsInRaid())
            return new();

        return scoreboard.Values
            .Where(s => s.Faction == faction)
            .Select(s => s.player)
            .ToList();
    }

    public List<PlayerScore> GetPlayerScoresFromFaction(Faction faction)
    {
        if (!H.IsInRaid()) return new();

        return scoreboard.Values.Where(s => s.Faction == faction).ToList();
    }
}

