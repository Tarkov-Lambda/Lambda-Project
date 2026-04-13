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
}