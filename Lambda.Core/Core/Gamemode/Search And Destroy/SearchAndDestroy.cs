using Comfort.Common;
using Cysharp.Threading.Tasks;
using Lambda.Core.Networking;
using ifp.arena.shared;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lambda.Core.Main.Gamemode;

public class SND_Cleanup : SharedCleanup
{
    public override void OnEnter()
    {
        foreach (var bombPlantZone in UnityEngine.Object.FindObjectsByType<BombPlantZone>(FindObjectsSortMode.None))
        {
            bombPlantZone.GetComponent<BoxCollider>().enabled = true;
        }

        H.Session.bombState = BombState.None;
        H.Arena.LastObjectivePlayer = null;
        H.Arena.LastObjectiveBombState = BombState.None;
        H.BombHandler?.Reset();

        if (H.IsServer)
        {
            SNDGamemode snd = H.Gamemode as SNDGamemode;
            int roundIndex = H.Session.GetRoundIndexOfTheCurrentHalf();
            double newTime = TimeOfDayHelper.GetMinutesForRound(roundIndex, snd.MaxRoundsToWin);
            Singleton<WeatherAndTimeSyncPacketHandler>.Instance.Send(newTime);
        }

        base.OnEnter();
    }

}

public class SND_Prepare : SharedPrepare
{
    public override void OnEnter()
    {
        base.OnEnter();
    }
}

public class SND_Action : IGameState
{
    public MatchState StateType => MatchState.RoundAction;
    public void OnEnter() { }
    public MatchState? OnUpdate()
    {
        if (!H.IsServer) return null;
        Faction? winner = CheckWipe();
        if (winner.HasValue)
        {
            H.Arena.Award(winner.Value, RoundWinReason.Elimination);
            return MatchState.RoundEnd;
        }
        if (H.Session.bombState == BombState.Planted)
        {
            return MatchState.RoundPlanted;
        }

        if (H.Arena.StateTimer <= 0)
        {
            H.Arena.Award(Faction.CT, RoundWinReason.Timeout);
            return MatchState.RoundEnd;
        }

        return null;
    }
    public void OnExit() { }

    private Faction? CheckWipe()
    {
        var alive = H.Scoreboard.Values.Where(p => p.IsAlive).GroupBy(p => p.Faction).ToDictionary(g => g.Key, g => g.Count());
        var factions = H.Scoreboard.Values.Select(p => p.Faction).Where(f => f != Faction.None && f != Faction.Spectator).Distinct();

        foreach (var f in factions)
        {
            if (!alive.ContainsKey(f) || alive[f] == 0)
            {
                return factions.FirstOrDefault(o => o != f);
            }
        }

        return null;
    }
}

public class SND_Planted : IGameState
{
    public MatchState StateType => MatchState.RoundPlanted;

    public void OnEnter() { }

    public MatchState? OnUpdate()
    {
        if (!H.IsServer) return null;

        // If all CT are dead before timer runs out
        if (!H.Scoreboard.Values.Any(p => p.IsAlive && p.Faction == Faction.CT))
        {
            H.Arena.Award(Faction.T, RoundWinReason.Elimination);
            return MatchState.RoundEnd;
        }

        if (H.Session.bombState == BombState.Defused)
        {
            H.Arena.Award(Faction.CT, RoundWinReason.Objective);
            return MatchState.RoundEnd;
        }

        if (H.Arena.StateTimer <= 0)
        {
            H.Arena.Award(Faction.T, RoundWinReason.Objective);
            Singleton<BombStatePacketHandler>.Instance.Send(H.Arena.LastObjectivePlayer, BombState.Exploded, Vector3.zero);
            return MatchState.RoundEnd;
        }

        return null;
    }

    public void OnExit() { }
}

public class SND_RoundEnd : SharedRoundEnd
{
    public override void OnExit()
    {
        base.OnExit();
    }
}

public class SNDGamemode : LambdaGamemode, IGMObjective, IGMRound, IGMSideSwappable, IGMBuyable, IGMWithNightMode
{
    public List<ILambdaObjective> Objectives { get; set; } = [];

    public int MaxRoundsToWin { get; set; } = 13;
    public int RoundsPerSide => MaxRoundsToWin - 1;

    public bool HasSideSwapped { get; set; } = false;

    public bool CanBuyInActivePhase { get; set; } = true;

#if DEBUG
    public int TimeInActivePhaseToBuy { get; set; } = 110;
#else
    public int TimeInActivePhaseToBuy { get; set; } = 30;
#endif

#if DEBUG
    public bool IsNightTime => H.Session.GetRoundIndexOfTheCurrentHalf() >= 9;
#else
    public bool IsNightTime => H.Session.GetRoundIndexOfTheCurrentHalf() >= 9;
#endif

    public static float platingTime = 4.5f;
    public static float defusingTime = 10f;
    public static float defuseRadius = 2.5f;

    public override Dictionary<MatchState, float> StateTimerConfig { get; } = new()
    {
        {MatchState.None, 0},
        {MatchState.Warmup, 120},
        {MatchState.WarmupEnd, 5},
        {MatchState.Cleanup, 3},
        {MatchState.Pause, 45},
#if DEBUG
        {MatchState.RoundPrepare, 5},
#else
        {MatchState.RoundPrepare, 15},
#endif
        {MatchState.RoundAction, 115},
        {MatchState.RoundEnd, 7},
        {MatchState.RoundPlanted, 45},
        {MatchState.SideSwap, 10},
        {MatchState.MatchEnd, 15}
    };

    public override IGameState CreateState(MatchState state) => state switch
    {
        MatchState.None         => new SharedNone(),
        MatchState.Warmup       => new SharedWarmup(),
        MatchState.WarmupEnd    => new SharedWarmupEnd(),
        MatchState.Cleanup      => new SND_Cleanup(),
        MatchState.Pause        => new SharedPause(),
        MatchState.RoundPrepare => new SND_Prepare(),
        MatchState.RoundAction  => new SND_Action(),
        MatchState.RoundPlanted => new SND_Planted(),
        MatchState.RoundEnd     => new SND_RoundEnd(),
        MatchState.SideSwap     => new SharedSideSwap(),
        MatchState.MatchEnd     => new SharedMatchEnd(),
        _ => null
    };
}
