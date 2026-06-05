using System.Collections.Generic;
using System.Linq;

namespace Lambda.Core.Main.Gamemode;

public class GunGameAction : IGameState
{
    public MatchState StateType => MatchState.RoundAction;
    public void OnEnter() { }
    public MatchState? OnUpdate()
    {
        if (H.IsClient) return null;
        if (H.Arena.StateTimer <= 0 || H.Scoreboard.Values.Any(p => p.Kills >= 20)) return MatchState.MatchEnd;
        return null;
    }
    public void OnExit() { }
}

public class GunGameGamemode : LambdaGamemode, IGMRespawnable
{
    public override string Name { get; } = "Gun Game";

    public int RespawnCost { get; set; } = 0;
    public IRespawnWeights RespawnWeights { get; } = new FFARespawnWeights();

    public override IGameState CreateState(MatchState state) => state switch
    {
        MatchState.None         => new SharedNone(),
        MatchState.Warmup       => new SharedWarmup(),
        MatchState.WarmupEnd    => new SharedWarmupEnd(),
        MatchState.RoundPrepare => new SharedPrepare(),
        MatchState.RoundAction  => new GunGameAction(),
        MatchState.MatchEnd     => new SharedMatchEnd(),
        _ => null
    };

    public override Dictionary<MatchState, float> StateTimerConfig { get; } = new()
    {
        {MatchState.None, 0},
        {MatchState.Warmup, 120},
        {MatchState.WarmupEnd, 5},
        {MatchState.Cleanup, 3},
        {MatchState.Pause, 45},
        {MatchState.RoundPrepare, 15},
        {MatchState.RoundAction, 600},
        {MatchState.RoundEnd, 8},
        {MatchState.RoundPlanted, 45},
        {MatchState.SideSwap, 10},
        {MatchState.MatchEnd, 15}
    };
}

