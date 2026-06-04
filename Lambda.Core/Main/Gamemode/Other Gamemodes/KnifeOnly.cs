using Cysharp.Threading.Tasks;
using EFT.InventoryLogic;
using Lambda.Core.Main.Economy;
using Lambda.Shared.Models;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lambda.Core.Main.Gamemode;

public class KnifeOnlyPrepare : SharedPrepare
{
    public override void OnEnter()
    {
        if (!H.IsHeadless)
        {
            if (H.MainPlayer.GetSlotItem(EquipmentSlot.Scabbard) == null)
            {
                IU.TryCreateItem(Hardcode.KNIFE, out Item knifeItem);
                IU.ClientRequestBuyItem(knifeItem).Forget();
            }
        }
        base.OnEnter();
    }

    public override void OnExit()
    {
        base.OnExit();
        if (!H.IsHeadless)
        {
            H.MainPlayer.SetFirstAvailableItem((result) =>
            {
                if (result.Failed)
                {
                    H.MainPlayer.SetEmptyHands(delegate { });
                }
            });
        }
    }
}

public class KnifeOnlyAction : IGameState
{
    public MatchState StateType => MatchState.RoundAction;
    public void OnEnter() { }
    public MatchState? OnUpdate()
    {
        Faction? winner = CheckWipe();
        if (winner.HasValue)
        {
            H.Arena.Award(winner.Value, RoundWinReason.Elimination);
            return MatchState.RoundEnd;
        }

        if (H.Arena.StateTimer <= 0)
        {
            Faction randomWinner = Random.Range(0, 2) == 0 ? Faction.CT : Faction.T;
            H.Arena.Award(randomWinner, RoundWinReason.Timeout);
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

public class KnifeOnlyGamemode : LambdaGamemode, IGMRound, IGMTeam
{
    public override string Name { get; } = "Knife Only";

    public int MaxRoundsToWin { get; set; } = 13;

    public override IGameState CreateState(MatchState state) => state switch
    {
        MatchState.None => new SharedNone(),
        MatchState.Warmup => new SharedWarmup(),
        MatchState.WarmupEnd => new SharedWarmupEnd(),
        MatchState.Cleanup => new SharedCleanup(),
        MatchState.Pause => new SharedPause(),
        MatchState.RoundPrepare => new KnifeOnlyPrepare(),
        MatchState.RoundAction => new KnifeOnlyAction(),
        MatchState.RoundEnd => new SharedRoundEnd(),
        MatchState.MatchEnd => new SharedMatchEnd(),
        _ => null
    };

    public override Dictionary<MatchState, float> StateTimerConfig { get; } = new()
    {
        {MatchState.None, 0},
        {MatchState.Warmup, 120},
        {MatchState.WarmupEnd, 5},
        {MatchState.Cleanup, 3},
        {MatchState.Pause, 600},
        {MatchState.RoundPrepare, 5},
        {MatchState.RoundAction, 115},
        {MatchState.RoundEnd, 8},
        {MatchState.SideSwap, 5},
        {MatchState.MatchEnd, 15}
    };
}
