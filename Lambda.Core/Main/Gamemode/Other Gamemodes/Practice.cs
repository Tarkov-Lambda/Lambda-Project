namespace Lambda.Core.Main.Gamemode;

public class PracticeModeRules : LambdaGamemode
{
    public override IInventoryManager InventoryManager => new BaseInventoryManager();

    public override AbstractMatchStateController CreateState(MatchState state) => state switch
    {
        MatchState.None => new SharedNone(),
        MatchState.Warmup => new SharedNone(),
        MatchState.WarmupEnd => new SharedNone(),
        MatchState.RoundPrepare => new SharedNone(),
        MatchState.RoundAction => new SharedNone(),
        MatchState.RoundEnd => new SharedNone(),
        _ => null
    };

}
