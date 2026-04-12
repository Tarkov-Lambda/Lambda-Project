using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core.Modding.Events;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using MemoryPack;
using System;
using UnityEngine;
using static Fika.Core.Modding.FikaEventDispatcher;

namespace ifp.arena.bep.Core.Gamemode;

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

[MemoryPackable]
public partial struct RoundActionPhaseEnd
{
    public int mvpId;
    public string mvpReason;
    public Faction winner;
    public RoundWinReason roundWinReason;
}

// This is the place where we manage both server/client arena behaviour
public class ArenaController : Singleton<ArenaController>, IDisposable
{
    public SessionInfo session;
    public GameModeRules ActiveRules { get; set; } = new SND_ModeRules();
    public EconomyManager EconomyManager = new();

    public float StateTimer;
    public double ServerPhaseStartSeconds, PhaseDurationSeconds;

    // This here is absolute bullshit
    public RoundActionPhaseEnd? PendingRoundActionEnd;
    public RoundActionPhaseEnd? LastRoundActionEnd;
    public int LastObjectivePlayerId = -1; // planter/defuser
    public BombState LastObjectiveBombState = BombState.None; // Defused/Exploded (or None)
    // End of absolute bullshit

    private IGameState _currentState;

    public GameObject _tickerObject;
    public GameObject _musicObject;

    public ArenaController()
    {
        if (H.GameWorld != null) StartSession(H.GameWorld);
        H.OnGameStarted += StartSession;
        H.OnGameDispose += EndSession;
        OnFikaEvent += ManageFikaEvents;
    }

    public void ManageFikaEvents(FikaEvent fikaEvent)
    {
        if (fikaEvent is PeerDisconnectedEvent peerDisconnectedEvent)
        {
            Player player = H.GetPlayer(peerDisconnectedEvent.Peer.Id);
            if (player != null)
            {
                if (H.IsClient) return;

                Singleton<PlayerKilledPacketHandler>.Instance.Send(Patch_Player_ShotReactions.LastDamageToPlayer[player]);
                // Singleton<PlayerReadinessPacketHandler>.Instance.SendForPlayer(player, PlayerReadinessState.Disconnected);
            }
        }
    }

    public void Dispose()
    {
        H.OnGameStarted -= StartSession;
        H.OnGameDispose -= EndSession;
        OnFikaEvent -= ManageFikaEvents;
        EndSession(H.GameWorld);
        session = null;
        Release(this);
    }

    public async void StartSession(GameWorld gameWorld)
    {
        if (!H.IsInRaid()) return;

        _tickerObject = new GameObject("Arena Gamesession");
        _tickerObject.GetOrAddComponent<GameModeTicker>();
        _tickerObject.GetOrAddComponent<TimeSyncTicker>();
        // _tickerObject.GetOrAddComponent<AudioSourceWorldDebug>();
        UnityEngine.Object.DontDestroyOnLoad(_tickerObject);

        session = new SessionInfo();

        if (!H.IsHeadless)
        {
            SteamAudioSourceAttacher.Initialize();

            HU.ApplyPainkiller();
            await Singleton<MapAssetBundleHandler>.Instance.LoadMap("lobby");
            Teleporter.Teleport(H.MainPlayer, "lobby");
            Singleton<BackendConfigSettingsClass>.Instance.AimPunchMagnitude = 1f;
            Physics.simulationMode = SimulationMode.FixedUpdate;
            Singleton<PlayerReadinessPacketHandler>.Instance.Send(PlayerReadinessState.Connected);
            SteamAudioInitializer.AttachListenerIfNeeded();
        }

        NetworkTime.Reset();
    }

    public void EndSession(GameWorld gameWorld)
    {
        Physics.simulationMode = SimulationMode.Script;
        // Cancel any in-flight ClientRequestGiveItem calls so they don't touch
        // inventory after the session has been torn down
        // IU.ResetInventoryLock();

        _currentState?.OnExit();
        _currentState = null;

        if (_tickerObject != null)
        {
            GameModeTicker.onUpdate = null;
            GameModeTicker.onLateUpdate = null;

            UnityEngine.Object.Destroy(_tickerObject);
            _tickerObject = null;

            UnityEngine.Object.Destroy(_musicObject);
            _musicObject = null;
        }
    }

    public void Update()
    {
        if (session == null || _currentState == null) return;

        // On Server: Start + Duration - Now ~= Duration (since Start is Now)
        // On Client: Start + Duration - Now = Remaining Time accurately synced
        StateTimer = (float)(ServerPhaseStartSeconds + PhaseDurationSeconds - NetworkTime.ServerNowSeconds);

        if (H.IsServer)
        {
            MatchState? nextState = _currentState.OnUpdate();
            if (nextState.HasValue)
            {
                ChangeState(nextState.Value);
            }
        }
    }

    // Server sends this
    public async void ChangeState(MatchState newStateType)
    {
        if (H.IsClient) return;

        RoundActionPhaseEnd? roundEndData = PendingRoundActionEnd;
        PendingRoundActionEnd = null;

        Singleton<MatchStateSyncPacketHandler>.Instance.Send(newStateType, H.Session.StateTimerConfig[newStateType], roundEndData);
    }

    // Everyone runs this when the match state packet is approved
    public void TransitionToState(MatchStateSyncPacket packet)
    {
        // Capture the previous state BEFORE updating any fields.
        // We assign _currentState and session fields first so that if OnExit() throws,
        // the state machine is already pointing at the new state and won't spam on
        // the next Update() tick.
        var previousState = _currentState;

        // Bootstrap the NTP offset from the packet's embedded current-server-time stamp.
        // This is critical for mid-session joiners who receive the MatchStateSyncPacket
        // before their first NTP roundtrip has completed (~100ms after joining).
        // BootstrapFromServerStamp is a no-op when HasSync is already true (no regression
        // for established players whose periodic NTP has already converged).
        if (!H.IsServer && packet.serverNowSeconds > 0)
        {
            NetworkTime.BootstrapFromServerStamp(packet.serverNowSeconds);
        }

        PhaseDurationSeconds = H.Session.StateTimerConfig[packet.matchState];
        ServerPhaseStartSeconds = packet.Timestamp;

        if (packet.roundActionEnd.HasValue)
        {
            LastRoundActionEnd = packet.roundActionEnd.Value;
            EventBus.OnRoundActionEnd?.Invoke(packet.roundActionEnd.Value);
        }

        session.matchState = packet.matchState;
        _currentState = ActiveRules.CreateState(packet.matchState);

        StateTimer = (float)(ServerPhaseStartSeconds + PhaseDurationSeconds - NetworkTime.ServerNowSeconds);

        // Exit the previous state now that _currentState is already advanced.
        if (previousState != null)
        {
            previousState.OnExit();
            EventBus.OnEnd?.Invoke(previousState.StateType);
        }

        D.LogArenaController($"Entering {_currentState.GetType()} at {NetworkTime.ServerNowSeconds}");

        if (_currentState != null)
        {
            _currentState.OnEnter();
            EventBus.OnEnter?.Invoke(_currentState.StateType);
        }
    }

    public void Award(Faction w, RoundWinReason reason)
    {
        if (!H.Session.factionWins.ContainsKey(w))
            H.Session.factionWins[w] = 0;
        H.Session.factionWins[w]++;

        var (mvpId, mvpReason) = MvpCalculator.CalculateRoundMvp(w, reason, H.Arena.LastObjectiveBombState, H.Arena.LastObjectivePlayerId);

        H.Arena.PendingRoundActionEnd = new RoundActionPhaseEnd { mvpId = mvpId, mvpReason = mvpReason, winner = w, roundWinReason = reason };
    }

    public void OnRoundEnd() => Singleton<SessionInfoPacketHandler>.Instance.Send();
}

public class GameModeTicker : MonoBehaviour
{
    public static Action onUpdate;
    public static Action onLateUpdate;

    private void Update()
    {
        onUpdate?.Invoke();
        Singleton<ArenaController>.Instance?.Update(); // null-guard: Instance is cleared before deferred Destroy fires
        EventBus.OnUpdate?.Invoke();
    }

    private void FixedUpdate()
    {
        onUpdate?.Invoke();
        // Singleton<ArenaController>.Instance.Update();
        EventBus.OnFixedUpdate?.Invoke();
    }

    private void LateUpdate()
    {
        onLateUpdate?.Invoke();
        EventBus.OnLateUpdate?.Invoke();

    }
}
