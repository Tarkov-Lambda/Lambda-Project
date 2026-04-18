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

using MemoryPack;

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
    public SessionManager session;
    public GameModeRules ActiveRules { get; set; } = new SND_ModeRules();

    public float StateTimer;
    public double ServerPhaseStartSeconds, PhaseDurationSeconds;

    // This here is absolute bullshit
    public RoundActionPhaseEnd? PendingRoundActionEnd;
    public RoundActionPhaseEnd? LastRoundActionEnd;
    public int LastObjectivePlayerId = -1; // planter/defuser
    public BombState LastObjectiveBombState = BombState.None; // Defused/Exploded (or None)
    // End of bullshit

    private IGameState _currentState;

    private TimeSyncTicker timeSyncTicker;

    public GameObject _musicObject;

    public ArenaController()
    {
        if (H.GameWorld != null) StartSession();
        H.OnGameStarted += StartSession;
        H.OnGameDispose += EndSession;
        OnFikaEvent += ManageFikaEvents;
    }

    public void Dispose()
    {
        H.OnGameStarted -= StartSession;
        H.OnGameDispose -= EndSession;
        OnFikaEvent -= ManageFikaEvents;
        EndSession();
        session = null;
        Release(this);
    }

    public void ManageFikaEvents(FikaEvent fikaEvent)
    {
        if (fikaEvent is PeerDisconnectedEvent peerDisconnectedEvent)
        {
            if (H.IsClient) return;

            // Player player = ;
            // if (player != null)
            // {
            //     Singleton<PlayerKilledPacketHandler>.Instance.Send(Patch_Player_ShotReactions.LastDamageToPlayer[player], player, player);

            //     player.GetScore().ChangeReadiness(PlayerReadinessState.Disconnected);
            //     player.GetScore().ChangeFaction(Faction.Spectator);
            //     // Singleton<PlayerReadinessPacketHandler>.Instance.SendForPlayer(player, PlayerReadinessState.Disconnected);
            // }
        }
    }

    public async void StartSession()
    {
        if (!H.IsInRaid()) return;

        UnityTicker.OnUpdate += Update;

        timeSyncTicker = new TimeSyncTicker();
        UnityTicker.OnUpdate += timeSyncTicker.Update;

        // _tickerObject.GetOrAddComponent<AudioSourceWorldDebug>();

        session = new SessionManager();

        if (!H.IsHeadless)
        {
            SteamAudioSourceAttacher.Initialize();

            HU.ApplyPainkiller();
            await Singleton<MapAssetBundleHandler>.Instance.LoadMap("lobby");
            Teleporter.Teleport(H.MainPlayer, "lobby", Faction.None);
            H.BackendConfigSettingsClass.AimPunchMagnitude = 1f;
            Physics.simulationMode = SimulationMode.FixedUpdate;
            Singleton<PlayerReadinessPacketHandler>.Instance.Send(PlayerReadinessState.Connected);
            SteamAudioInitializer.AttachListenerIfNeeded();

            PU.OpenEyes();
        }

        NetworkTime.Reset();
    }

    public void EndSession()
    {
        Physics.simulationMode = SimulationMode.Script;

        _currentState?.OnExit();
        _currentState = null;

        UnityTicker.OnUpdate -= Update;

        if (timeSyncTicker != null)
        {
            UnityTicker.OnUpdate -= timeSyncTicker.Update;
            timeSyncTicker.Dispose();
        }

        if (_musicObject != null)
        {
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
        // cache current state
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

#if DEBUG
        D.LogArenaController($"Entering {_currentState.GetType()} at {NetworkTime.ServerNowSeconds}");
#endif

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

    public void OnRoundEnd() => Singleton<SessionManagerSyncPacketHandler>.Instance.Send();
}

public class UnityTicker : MonoBehaviour
{
    public static event Action OnUpdate;
    public static event Action OnLateUpdate;

    private void Update() => OnUpdate?.Invoke();
    private void LateUpdate() => OnLateUpdate?.Invoke();
}
