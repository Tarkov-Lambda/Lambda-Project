using Comfort.Common;
using Cysharp.Threading.Tasks;
using Lambda.Core.Main.FX;
using Lambda.Core.Networking;
using Lambda.Shared;
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
            Singleton<WeatherAndTimeSyncPacketWarden>.Instance.Send(newTime);
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
        int aliveCT = 0;
        int aliveT = 0;
        foreach (var p in H.Scoreboard.Values)
        {
            if (p.IsAlive && p.Faction == Faction.CT) aliveCT++;
            if (p.IsAlive && p.Faction == Faction.T) aliveT++;
        }

        if (aliveCT == 0) return Faction.T;
        if (aliveT == 0) return Faction.CT;
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
            Singleton<BombStatePacketWarden>.Instance.Send(H.Arena.LastObjectivePlayer, BombState.Exploded, BombHandler.Instance.BombPlantedPosition);
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
    public override string Name { get; } = "Search And Destroy";

    public List<ILambdaObjective> Objectives { get; set; } = [];

    public int MaxRoundsToWin { get; set; } = 13;
    public int RoundsPerSide => MaxRoundsToWin - 1;

    public bool HasSideSwapped { get; set; } = false;

    public bool CanBuyInActivePhase { get; set; } = true;

    public int TimeInActivePhaseToBuy { get; set; } = 20;

    public bool IsNightTime => H.Session.GetRoundIndexOfTheCurrentHalf() >= 9;

    public static float platingTime = 4.5f;
    public static float defusingTime = 10f;
    public static float defuseRadius = 2.5f;

    public override Dictionary<MatchState, float> StateTimerConfig { get; } = new()
    {
        {MatchState.None, 0},
        {MatchState.Warmup, 120},
        {MatchState.WarmupEnd, 5},
        {MatchState.Cleanup, 5},
        {MatchState.Pause, 600},
        {MatchState.RoundPrepare, 15},
        {MatchState.RoundAction, 115},
        {MatchState.RoundEnd, 7},
        {MatchState.RoundPlanted, 45},
        {MatchState.SideSwap, 5},
        {MatchState.MatchEnd, 15}
    };

    public override IGameState CreateState(MatchState state) => state switch
    {
        MatchState.None => new SharedNone(),
        MatchState.Warmup => new SharedWarmup(),
        MatchState.WarmupEnd => new SharedWarmupEnd(),
        MatchState.Cleanup => new SND_Cleanup(),
        MatchState.Pause => new SharedPause(),
        MatchState.RoundPrepare => new SND_Prepare(),
        MatchState.RoundAction => new SND_Action(),
        MatchState.RoundPlanted => new SND_Planted(),
        MatchState.RoundEnd => new SND_RoundEnd(),
        MatchState.SideSwap => new SharedSideSwap(),
        MatchState.MatchEnd => new SharedMatchEnd(),
        _ => null
    };
}
