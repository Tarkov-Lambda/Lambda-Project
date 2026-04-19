using System.Collections.Generic;
using UnityEngine;

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
/// Whether this gamemode allows respawning during action phase
/// </summary>
public interface IObjectiveBased
{
    List<ILambdaObjective> ObjectivePositions { get; set; }
}

/// <summary>
/// Whether this gamemode allows respawning during action phase
/// </summary>
public interface ISingularObjectiveBased : IObjectiveBased
{
    ILambdaObjective CurrentObjective { get; set; }
}

/// <summary>
/// Whether this gamemode allows respawning during action phase
/// </summary>
public interface IRespawnable
{
    // For shit like conquest, default is 0
    public int RespawnCost { get; set; }
    // Configuration
    public IRespawnWeights RespawnWeights { get; }
}

/// <summary>
/// Whether this gamemode has team side swapping
/// </summary>
public interface ISideSwappable : IRoundBased, ITeamBased
{
    public bool HasSideSwapped { get; set; }
}

// Search And Destroy   multiple active
// Hardpoint            singular active
// Domination           multiple active
// Conquest             multiple active
// FFA
// TDM
// Gun Game
// Duel
// TRG Only