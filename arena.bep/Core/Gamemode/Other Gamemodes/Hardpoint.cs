using System.Collections.Generic;
using System.Linq;

namespace ifp.arena.bep.Core.Gamemode;

public class HardpointAction : IGameState
{
    public MatchState StateType => MatchState.RoundAction;
    public void OnEnter() { }
    public MatchState? OnUpdate()
    {
        if (H.IsClient) return null;
        if (H.Arena.StateTimer <= 0 || H.Session.factionWins.Values.Any(faction => faction == 150)) return MatchState.MatchEnd;
        return null;
    }
    public void OnExit() { }
}

public class HardpointGamemode : LambdaGamemode, IGMTeam, IGMObjective
{
    public List<ILambdaObjective> Objectives { get; set; } = [];

    public override IGameState CreateState(MatchState state) => state switch
    {
        MatchState.None         => new SharedNone(),
        MatchState.Warmup       => new SharedWarmup(),
        MatchState.WarmupEnd    => new SharedWarmupEnd(),
        MatchState.RoundPrepare => new SharedPrepare(),
        MatchState.RoundAction  => new HardpointAction(),
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