using EFT;
using Lambda.Core.Main;
using Lambda.Shared;
using System;
using System.Collections.Generic;

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
                    scoreboard.Add(p.Id, new PlayerContext(p.Id));
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

        int totalRoundsPlayed = TotalRoundsPlayed();

        int roundsPerSide = roundMode.RoundsPerSide;

        return totalRoundsPlayed % roundsPerSide;
    }

    public int TotalRoundsPlayed()
    {
        int total = 0;
        foreach (int wins in factionWins.Values) total += wins;
        return total;
    }

    public List<Player> GetPlayersFromFaction(Faction faction)
    {
        if (!H.IsArenaReady) return [];

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
        if (!H.IsArenaReady) return [];

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

#nullable enable

// Indexed by Player.Id (Fika NetId), grows when an id exceeds the current capacity
public class PlayerContextLookup(int capacity)
{
    private PlayerContext?[] _items = new PlayerContext?[capacity + 1];

    private readonly List<PlayerContext> _activeContexts = new();

    public int Count => _activeContexts.Count;

    public PlayerContext? this[int id] => IsInRange(id) ? _items[id] : null;

    public List<PlayerContext> Values => _activeContexts;

    public bool TryGetValue(int id, out PlayerContext? value)
    {
        value = this[id];
        return value is not null;
    }

    public bool ContainsKey(int id) => this[id] is not null;

    public void Add(int id, PlayerContext value)
    {
        if (id < 0) return;

        if (id >= _items.Length)
            Array.Resize(ref _items, Math.Max(id + 1, _items.Length * 2));

        if (_items[id] is { } existing)
            _activeContexts.Remove(existing);

        _items[id] = value;
        _activeContexts.Add(value);
    }

    public bool Remove(int id)
    {
        var item = this[id];

        if (item is null)
            return false;

        _activeContexts.Remove(item);
        _items[id] = null;
        return true;
    }

    private bool IsInRange(int id) => (uint)id < (uint)_items.Length;
}