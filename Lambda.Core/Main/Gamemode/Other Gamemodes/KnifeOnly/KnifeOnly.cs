using Cysharp.Threading.Tasks;
using EFT.InventoryLogic;
using Lambda.Core.Main.Economy;
using Lambda.Shared.Models;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lambda.Core.Main.Gamemode;

public class KnifeOnlyGamemode : LambdaGamemode, IGMRound, IGMTeam
{
    public KnifeOnlyGamemode()
    {
        H.Arena.inventoryManager = new KnifeOnlyInventoryManager();
    }

    public override string Name { get; } = "Knife Only";

    public int MaxRoundsToWin { get; set; } = 13;

    public override AbstractMatchStateController CreateState(MatchState state) => state switch
    {
        MatchState.None => new SharedNone(),
        MatchState.Warmup => new SharedWarmup(),
        MatchState.WarmupEnd => new SharedWarmupEnd(),
        MatchState.Cleanup => new SharedCleanup(),
        MatchState.Pause => new SharedPause(),
        MatchState.RoundPrepare => new SharedPrepare(),
        MatchState.RoundAction => new GenericTeamRoundBasedAction(),
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
