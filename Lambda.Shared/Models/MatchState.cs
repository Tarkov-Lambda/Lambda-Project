public enum MatchState : byte
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

    // Extra
    Alpha,
    Beta,
    Charlie,
    Delta,
    Echo,
    Foxtrot,
    Golf,
    Hotel,
    India,
    Juliett,
    Kilo,
    Lima,
    Mike,
    November,
    Oscar
}