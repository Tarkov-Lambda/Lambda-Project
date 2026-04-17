public enum MatchState
{
    // Just chilling type beat
    None,

    // Waiting for players to load
    Warmup,
    WarmupEnd,

    Cleanup,

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