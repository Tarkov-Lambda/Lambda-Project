using EFT;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lambda.Core.GameTypes;

public class SessionManager
{
    public MatchState matchState = MatchState.None;
    public Dictionary<int, PlayerScore> scoreboard = new();
    public Dictionary<Faction, int> factionWins = new();
    public BombState bombState = BombState.None;

    public string level = "";

    public int mvpId;

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

    public int GetRoundIndexOfTheCurrentHalf()
    {
        if (H.Gamemode is not IGMRound roundMode || H.Gamemode is not IGMSideSwappable sideMode)
            return 0;

        int totalRoundsPlayed = factionWins.Values.Sum();

        int roundsPerSide = roundMode.RoundsPerSide;

        return totalRoundsPlayed % roundsPerSide;
    }

    public List<Player> GetPlayersFromFaction(Faction faction)
    {
        if (!H.IsInRaid()) return [];

        var result = new List<Player>();

        foreach (var s in scoreboard.Values)
        {
            if (s.Faction == faction)
            {
                result.Add(s.player);
            }
        }

        return result;
    }

    public List<PlayerScore> GetPlayerScoresFromFaction(Faction faction)
    {
        if (!H.IsInRaid()) return [];

        var result = new List<PlayerScore>();

        foreach (var s in scoreboard.Values)
        {
            if (s.Faction == faction)
            {
                result.Add(s);
            }
        }

        return result;
    }
}

