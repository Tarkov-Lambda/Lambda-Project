using System.Collections.Generic;
using System.Linq;

namespace Lambda.Core.Main.Gamemode;

public class HardpointAction : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.RoundAction;
    public override void OnEnter() { }
    public override MatchState? OnUpdate()
    {
        if (H.IsClient) return null;
        if (H.Arena.StateTimer <= 0 || H.Session.factionWins.Values.Any(faction => faction == 150)) return MatchState.MatchEnd;
        return null;
    }
    public override void OnExit() { }
}

public class HardpointGamemode : LambdaGamemode, IGMTeam, IGMObjective
{
    public override IInventoryManager InventoryManager => new BaseInventoryManager();

    public override string Name { get; } = "King Of The Hill";

    public List<ILambdaObjective> Objectives { get; set; } = [];

    public override AbstractMatchStateController CreateState(MatchState state) => state switch
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