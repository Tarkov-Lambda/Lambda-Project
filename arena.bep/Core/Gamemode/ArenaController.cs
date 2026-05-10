using arena.ui.scoreboard;
using Comfort.Common;
using EFT;
using Fika.Core.Modding.Events;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.UI;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.shared;
using MemoryPack;
using System;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using static Fika.Core.Modding.FikaEventDispatcher;

namespace ifp.arena.bep.Core.Gamemode;

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
    public SessionManager Session { get; private set; } = null;
    public LambdaGamemode gamemode = null;
    public EconomyManager economyManager = new();
    public RespawnManager respawnManager = new();

    private GameObject _hideoutLight;

    public float StateTimer;
    public double ServerPhaseStartSeconds, PhaseDurationSeconds;

    // this block of vars just seems wrong
    public RoundActionPhaseEnd? PendingRoundActionEnd;
    public RoundActionPhaseEnd? LastRoundActionEnd;
    public Player LastObjectivePlayer;
    public BombState LastObjectiveBombState = BombState.None; // Defused/Exploded (or None)

    private IGameState _currentState;

    private TimeSyncTicker _timeSyncTicker;

    public event Action OnArenaBeginInitializing;
    public event Action OnArenaInitialized;
    public event Action OnArenaBeginDisposing;
    public event Action OnArenaDisposed;

    public ArenaController()
    {
        if (H.IsInRaid()) StartSession();
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
        Release(this);
    }

    public void ManageFikaEvents(FikaEvent fikaEvent)
    {
        if (fikaEvent is PeerDisconnectedEvent peerDisconnectedEvent)
        {
            if (H.IsClient) return;

            Player player = peerDisconnectedEvent.Peer.Player;
            if (player != null)
            {
                Singleton<PlayerReadinessPacketHandler>.Instance.SendForPlayer(player, PlayerReadinessState.Disconnected);
                var damageInfo = new DamageInfoStruct
                {
                    Damage = 1f,
                    BodyPartColliderType = EBodyPartColliderType.RibcageUp
                };
                Singleton<PlayerKilledPacketHandler>.Instance.Send(damageInfo, player, player);
                // Singleton<DictateTeleportHandler>.Instance.SendToPlayer();
            }
        }
    }

    public async void StartSession()
    {
        if (!H.IsInRaid()) return;

        UnityTicker.OnUpdate += Update;

        _timeSyncTicker = new TimeSyncTicker();
        UnityTicker.OnUpdate += _timeSyncTicker.Update;

        Session = new SessionManager();

        if (!H.IsHeadless)
        {
            SteamAudioInitializer.AttachListenerIfNeeded();

            await Singleton<MapAssetBundleHandler>.Instance.LoadMap("lobby");
            Teleporter.Teleport(H.MainPlayer, "lobby", Faction.None);

            PresetBundleHandler.Instance.AddToCache(PresetItemsCache.Instance.GetAllPresetItems());
            DefaultEquipmentManager.Instance.CapturePreset();

            if (H.GameWorld is not HideoutGameWorld)
            {
                HU.ApplyPainkiller();
            }
            else
            {
                CreateFullBrightHack();
            }

            H.BackendConfigSettingsClass.AimPunchMagnitude = 1f;
            H.Session.scoreboard[H.MainPlayer.Id] = new PlayerScore(H.MainPlayer.Id);
            H.MainPlayerScore.Spawn();

            Singleton<PlayerReadinessPacketHandler>.Instance.Send(PlayerReadinessState.Connected);

            PU.OpenEyes();
        }

        NetworkTime.Reset();
    }

    void CreateFullBrightHack()
    {
        _hideoutLight = new GameObject("hideoutlight");

        CreateDirLight(_hideoutLight.transform, new Vector3(90, 0, 0));
        CreateDirLight(_hideoutLight.transform, new Vector3(-90, 0, 0));
        CreateDirLight(_hideoutLight.transform, new Vector3(0, 90, 0));
        CreateDirLight(_hideoutLight.transform, new Vector3(0, -90, 0));
    }

    void CreateDirLight(Transform parent, Vector3 rotation)
    {
        var go = new GameObject("DirLight");
        go.transform.parent = parent;
        go.transform.rotation = Quaternion.Euler(rotation);

        var light = go.AddComponent<Light>();
        light.type = UnityEngine.LightType.Directional;

        light.intensity = 0.5f;
        light.shadows = LightShadows.None;
    }

    public void EndSession()
    {
        Physics.simulationMode = SimulationMode.Script;

        _currentState?.OnExit();
        _currentState = null;

        Session = null;

        UnityTicker.OnUpdate -= Update;

        if (_timeSyncTicker != null)
        {
            UnityTicker.OnUpdate -= _timeSyncTicker.Update;
            _timeSyncTicker.Dispose();
        }

        if (_hideoutLight != null)
        {
            GameObject.Destroy(_hideoutLight);
        }
    }

    public void Update()
    {
        if (Session == null || _currentState == null) return;

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

        Singleton<MatchStateSyncPacketHandler>.Instance.Send(newStateType, H.Gamemode.StateTimerConfig[newStateType], roundEndData);
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

        PhaseDurationSeconds = H.Gamemode.StateTimerConfig[packet.matchState];
        ServerPhaseStartSeconds = packet.Timestamp;

        if (packet.roundActionEnd.HasValue)
        {
            LastRoundActionEnd = packet.roundActionEnd.Value;
            EventBus.OnRoundActionEnd?.Invoke(packet.roundActionEnd.Value);
        }

        Session.matchState = packet.matchState;
        _currentState = gamemode.CreateState(packet.matchState);

        StateTimer = (float)(ServerPhaseStartSeconds + PhaseDurationSeconds - NetworkTime.ServerNowSeconds);

        // Exit the previous state now that _currentState is already advanced.
        if (previousState != null)
        {
            previousState.OnExit();
            EventBus.OnExit?.Invoke(previousState.StateType);
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

        var (mvpId, mvpReason) = MvpCalculator.CalculateRoundMvp(w, reason, H.Arena.LastObjectiveBombState, H.Arena.LastObjectivePlayer);

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
