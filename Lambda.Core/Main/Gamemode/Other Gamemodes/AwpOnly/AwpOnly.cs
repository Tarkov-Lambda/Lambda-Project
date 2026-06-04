using Cysharp.Threading.Tasks;
using EFT.InventoryLogic;
using Lambda.Core.Main.Economy;
using Lambda.Shared.Models;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lambda.Core.Main.Gamemode;

public class AwpOnlyGamemode : LambdaGamemode, IGMRound, IGMTeam
{
    public AwpOnlyGamemode()
    {
        H.Arena.inventoryManager = new AwpOnlyInventoryManager();
    }

    public override string Name { get; } = "AWP Only";

    public int MaxRoundsToWin { get; set; } = 13;

    public override IGameState CreateState(MatchState state) => state switch
    {
        MatchState.None => new SharedNone(),
        MatchState.Warmup => new SharedWarmup(),
        MatchState.WarmupEnd => new SharedWarmupEnd(),
        MatchState.Cleanup => new SharedCleanup(),
        MatchState.Pause => new SharedPause(),
        MatchState.RoundPrepare => new SharedPrepare(),
        MatchState.RoundAction => new GenericRoundBasedAction(),
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
