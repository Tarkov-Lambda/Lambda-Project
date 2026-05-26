using EFT;
using Lambda.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lambda.Core.GameTypes;

public class SessionManager
{
    public MatchState matchState = MatchState.None;
    public PlayerContextLookup scoreboard = new(256);
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
                    scoreboard[p.Id] = new PlayerContext(p.Id);
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

    public List<PlayerContext> GetPlayerScoresFromFaction(Faction faction)
    {
        if (!H.IsInRaid()) return [];

        var result = new List<PlayerContext>();

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

// sloppy and crude
public class PlayerContextLookup(int maxId)
{
    private readonly PlayerContext?[] _items = new PlayerContext?[maxId + 1];

    private int _count;

    public int Count => _count;

    public PlayerContext? this[int id]
    {
        get => _items[id];
        set => _items[id] = value;
    }

    public IEnumerable<PlayerContext> Values
    {
        get
        {
            foreach (var item in _items)
            {
                if (item is not null)
                    yield return item;
            }
        }
    }

    public IEnumerable<(int Id, PlayerContext Context)> Entries
    {
        get
        {
            for (int i = 0; i < _items.Length; i++)
            {
                var item = _items[i];

                if (item is not null)
                    yield return (i, item);
            }
        }
    }

    public bool TryGetValue(int id, out PlayerContext? value)
    {
        value = _items[id];
        return value is not null;
    }

    public bool ContainsKey(int id)
    {
        return _items[id] is not null;
    }

    public void Add(int id, PlayerContext value)
    {
        if (_items[id] is not null)
            throw new ArgumentException($"ID {id} already exists");

        _items[id] = value;
        _count++;
    }

    public bool Remove(int id)
    {
        if (_items[id] is null)
            return false;

        _items[id] = default;
        _count--;
        return true;
    }
}