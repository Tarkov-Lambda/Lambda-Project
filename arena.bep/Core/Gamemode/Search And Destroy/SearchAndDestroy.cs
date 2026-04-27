using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ifp.arena.bep.Core.Gamemode;

public class SND_Cleanup : SharedCleanup
{
    public override void OnEnter()
    {
        foreach (var bombPlantZone in UnityEngine.Object.FindObjectsByType<BombPlantZone>(FindObjectsSortMode.None))
        {
            bombPlantZone.GetComponent<BoxCollider>().enabled = true;
        }

        H.Session.bombState = BombState.None;
        H.Arena.LastObjectivePlayerId = -1;
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
            Player lastObjectivePlayer = H.GetPlayer(H.Arena.LastObjectivePlayerId);
            Singleton<BombStatePacketHandler>.Instance.Send(lastObjectivePlayer, BombState.Exploded, Vector3.zero);
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

public class SNDGamemode : LambdaGamemode, IGMObjective, IGMRound, IGMSideSwappable, IGMBuyable
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

    public bool IsNightTime => H.Session.GetRoundIndexOfTheCurrentHalf() >= 9;

    public static float platingTime = 4.5f;
    public static float defusingTime = 10f;
    public static float defuseRadius = 2.5f;
    public static string bombTemplateId = "628bc7fb408e2b2e9c0801b1";
    public static string defuseKitTemplateId = "544fb5454bdc2df8738b456a";

    public new Dictionary<MatchState, float> StateTimerConfig = new()
    {
        {MatchState.None, 0},
        {MatchState.Warmup, 120},
        {MatchState.WarmupEnd, 5},
        {MatchState.Cleanup, 3},
        {MatchState.Pause, 45},
        {MatchState.RoundPrepare, 15},
        {MatchState.RoundAction, 115},
        {MatchState.RoundEnd, 8},
        {MatchState.RoundPlanted, 45},
        {MatchState.SideSwap, 10},
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
        MatchState.MatchEnd => new SharedFinish(),
        _ => null
    };
}
