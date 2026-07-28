using System;
using System.Collections.Generic;
using System.Threading;

public abstract class AbstractMatchStateController : IDisposable
{
    // This CTS is intended for cancelling asynchronous OnEnter events in case the server
    // forcibly pulls us out of the current Match State Controller into another one
    // for example during Session Stop Packet Application
    protected CancellationTokenSource _cts = new();
    public CancellationToken MatchStateCancellationToken => _cts.Token;

    public abstract MatchState StateType { get; }
    public abstract void OnEnter();
    public abstract MatchState? OnUpdate(); // Returns next state, or null to stay
    public abstract void OnExit();

    public virtual void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

public abstract class LambdaGamemode : IGamemode
{
    public virtual string Name { get; } = "Generic Lambda Gamemode";

    public abstract IInventoryManager InventoryManager { get; }

    public abstract AbstractMatchStateController CreateState(MatchState state);

    public virtual Dictionary<MatchState, float> StateTimerConfig { get; } = new()
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

    public virtual void Dispose() { }
}

public interface IGamemode : IDisposable
{
    public string Name { get; }
    public abstract AbstractMatchStateController CreateState(MatchState state);
    public Dictionary<MatchState, float> StateTimerConfig { get; }
    public IInventoryManager InventoryManager { get; }
}

/// <summary>
/// Whether or not there is an economy system
/// </summary>
public interface IGMBuyable
{
    // TODO: Refactor EconomyManager to be more moldable (granted idk if that's needed for anything besides SND)
    public bool CanBuyInActivePhase { get; set; }
    public int TimeInActivePhaseToBuy { get; set; }
}

/// <summary>
/// Whether this gamemode has a multi-round gameplay loop
/// </summary>
public interface IGMRound
{
    public int MaxRoundsToWin { get; set; }
    public int RoundsPerSide => MaxRoundsToWin - 1;
}

/// <summary>
/// Whether this gamemode has night mode
/// </summary>
public interface IGMWithNightMode
{
    public bool IsNightTime { get; }
}

/// <summary>
/// Whether it's a CT/T gamemode
/// </summary>
public interface IGMTeam
{

}

/// <summary>
/// Whether this gamemode allows respawning during action phase
/// </summary>
public interface IGMObjective
{
    List<ILambdaObjective> Objectives { get; set; }
}

/// <summary>
/// Whether this gamemode allows respawning during action phase
/// </summary>
public interface IGMSingularActiveObjective : IGMObjective
{
    ILambdaObjective CurrentObjective { get; set; }
}

/// <summary>
/// Whether this gamemode allows respawning during action phase
/// </summary>
public interface IGMRespawnable
{
    // For shit like conquest, default is 0
    public int RespawnCost { get; set; }
    // Configuration
    public IRespawnWeights RespawnWeights { get; }
}

/// <summary>
/// Whether this gamemode has team side swapping
/// </summary>
public interface IGMSideSwappable : IGMRound, IGMTeam
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