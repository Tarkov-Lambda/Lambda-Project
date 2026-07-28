using System.Collections.Generic;
using UnityEngine;

namespace Lambda.Core.Main.Gamemode;

public enum RoundGunType
{
    TRG,
    MK18,
    AK50
}

public class AwpOnlyCleanup : SharedCleanup
{
    public override void OnEnter()
    {
        if (H.Arena.gamemode is AwpOnlyGamemode awpGamemode)
        {
            float roll = Random.value;

            awpGamemode.RoundGunType = roll switch
            {
                < 0.80f => RoundGunType.TRG,
                < 0.9f => RoundGunType.MK18,
                _ => RoundGunType.AK50
            };
        }

        base.OnEnter();
    }
}

public class AwpOnlyGamemode : LambdaGamemode, IGMRound, IGMTeam
{
    public override IInventoryManager InventoryManager => new AwpOnlyInventoryManager();

    public override string Name { get; } = "AWP Only";

    public int MaxRoundsToWin { get; set; } = 13;

    public RoundGunType RoundGunType = RoundGunType.TRG;

    public override AbstractMatchStateController CreateState(MatchState state) => state switch
    {
        MatchState.None => new SharedNone(),
        MatchState.Warmup => new SharedWarmup(),
        MatchState.WarmupEnd => new SharedWarmupEnd(),
        MatchState.Cleanup => new AwpOnlyCleanup(),
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
        {MatchState.Cleanup, 2},
        {MatchState.Pause, 600},
        {MatchState.RoundPrepare, 3},
        {MatchState.RoundAction, 115},
        {MatchState.RoundEnd, 3},
        {MatchState.SideSwap, 5},
        {MatchState.MatchEnd, 15}
    };
}
