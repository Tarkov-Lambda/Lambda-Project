namespace ifp.arena.bep.GameTypes;

public enum MatchState
{
    // Just chilling type beat
    None,

    // Waiting for players to load
    Warmup,
    WarmupEnd,

    // Shared
    Pause, // Can only be invoked when in RoundPrepare
    RoundPrepare,
    RoundAction,
    RoundEnd,

    // SND
    RoundPlanted,

    // Probably a good way to do these
    SideSwap,
    MatchEnd,
}

public enum PlayerReadinessState
{
    Disconnected,
    Connected,
    Ready
}

public enum BombState
{
    None,
    Planting,
    Planted,
    Defusing,
    Defused,
    Exploded
}

public enum RoundWinReason
{
    None,
    Objective,
    Elimination,
    Timeout
}

