using System.Collections.Generic;

public interface IGameState
{
    MatchState StateType { get; }
    void OnEnter();
    MatchState? OnUpdate(); // Returns next state, or null to stay
    void OnExit();
}

public abstract class GameModeRules
{
    public abstract IGameState CreateState(MatchState state);

    public Dictionary<MatchState, float> StateTimerConfig = new()
    {
        {MatchState.None, 0},
        {MatchState.Warmup, 120},
        {MatchState.WarmupEnd, 5},
        {MatchState.Cleanup, 3},
        {MatchState.Pause, 45},
        {MatchState.RoundPrepare, 5},
        {MatchState.RoundAction, 115},
        {MatchState.RoundEnd, 8},
        {MatchState.RoundPlanted, 45},
        {MatchState.SideSwap, 10},
        {MatchState.MatchEnd, 15}
    };
}

/// <summary>
/// Whether or not there is an economy system
/// </summary>
public interface IBuyable
{
    // TODO: Refactor EconomyManager to be more moldable (granted idk if that's needed for anything besides SND)
    public bool CanBuyInActivePhase { get; set; }
    public int TimeInActivePhaseToBuy { get; set; }
}

/// <summary>
/// Whether this gamemode has a multi-round gameplay loop
/// </summary>
public interface IRoundBased
{
    public int MaxRoundsToWin { get; set; }
}

/// <summary>
/// Whether it's a CT/T gamemode
/// </summary>
public interface ITeamBased
{
    
}

/// <summary>
/// Whether this gamemode has team side swapping
/// </summary>
public interface ISideSwappable : IRoundBased, ITeamBased
{
    public bool HasSideSwapped { get; set; }
}