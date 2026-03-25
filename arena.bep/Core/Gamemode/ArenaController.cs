using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core.Main.Utils;
using ifp.arena.bep;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.shared;
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.Core.Gamemode
{
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

    public static class EventBus
    {
        public static Action<MatchState> OnEnter;
        // We do not have OnUpdate action because clients don't run the update loop 
        public static Action<MatchState> OnEnd;
        public static Action<BombState> OnBombStateChange;
        public static Action<PlayerKilledPacket> OnPlayerKill;

        public static Action OnUpdate;
        public static Action OnLateUpdate;
        public static Action OnFixedUpdate;

        public static Action<RoundActionPhaseEnd> OnRoundActionEnd;
        public static Action<int> OnSelfMoneyChanged;

        public static Action OnItemBuy;

        public static Action OnSelfRespawn;
    }

    [MemoryPackable]
    public partial struct RoundActionPhaseEnd
    {
        public int mvpId;
        public Faction winner;
        public RoundWinReason roundWinReason;
    }

    // This is the place where we manage both server/client arena behaviour
    public class ArenaController : Singleton<ArenaController>, IDisposable
    {
        public SessionInfo session;
        public GameModeRules ActiveRules { get; set; } = new SnDModeRules();
        public EconomyManager EconomyManager = new();

        public float StateTimer;
        public double ServerPhaseStartSeconds, PhaseDurationSeconds;

        public event Action OnUpdateTick;

        // This here is absolute bullshit
        public RoundActionPhaseEnd? PendingRoundActionEnd;
        public RoundActionPhaseEnd? LastRoundActionEnd;
        public int LastObjectivePlayerId = -1; // planter/defuser
        public BombState LastObjectiveBombState = BombState.None; // Defused/Exploded (or None)
        // End of absolute bullshit

        private IGameState _currentState;

        private GameObject _tickerObject;
        public GameObject _musicObject;

        public ArenaController()
        {
            if (H.GameWorld != null) StartSession(H.GameWorld);
            H.OnGameStarted += StartSession;
            H.OnGameDispose += EndSession;
        }

        public void Dispose()
        {
            H.OnGameStarted -= StartSession;
            H.OnGameDispose -= EndSession;
            EndSession(H.GameWorld);
            Release(this);
        }

        public async void StartSession(GameWorld gameWorld)
        {
            if (H.GameWorld is HideoutGameWorld) return;

            IU.ResetInventoryLock();

            _tickerObject = new GameObject("Arena Gamesession");
            _tickerObject.AddComponent<GameModeTicker>();
            _tickerObject.AddComponent<TimeSyncTicker>();
            UnityEngine.Object.DontDestroyOnLoad(_tickerObject);

            HU.ApplyPainkiller();

            D.Notify("Plugin Reloaded");
            if (session == null) session = new SessionInfo();

            await Singleton<MapAssetBundleHandler>.Instance.LoadMap("lobby");
            Teleporter.Teleport(H.MainPlayer, "lobby");

            Singleton<BackendConfigSettingsClass>.Instance.AimPunchMagnitude = 1f;


            Physics.simulationMode = SimulationMode.FixedUpdate;
            // delay is stupid
            if (FikaBackendUtils.IsClient)
            {
                await UniTask.Delay(200);
                Singleton<AdminLoginPacketHandler>.Instance.Send();
            }

        }

        public void EndSession(GameWorld gameWorld)
        {
            Physics.simulationMode = SimulationMode.Script;
            // Cancel any in-flight ClientRequestGiveItem calls so they don't touch
            // inventory after the session has been torn down
            IU.ResetInventoryLock();

            if (_tickerObject != null)
            {
                UnityEngine.Object.Destroy(_tickerObject);
                UnityEngine.Object.Destroy(_musicObject);
            }
        }

        public void Update()
        {
            if (session == null || _currentState == null) return;

            // On Server: Start + Duration - Now ~= Duration (since Start is Now)
            // On Client: Start + Duration - Now = Remaining Time accurately synced
            StateTimer = (float)(ServerPhaseStartSeconds + PhaseDurationSeconds - NetworkTime.ServerNowSeconds);

            if (FikaBackendUtils.IsServer)
            {
                MatchState? nextState = _currentState.OnUpdate();
                if (nextState.HasValue)
                {
                    ChangeState(nextState.Value);
                }
            }

            OnUpdateTick?.Invoke();
        }

        // Server sends this
        public void ChangeState(MatchState newStateType)
        {
            RoundActionPhaseEnd? roundEndData = PendingRoundActionEnd;
            PendingRoundActionEnd = null;

            Singleton<MatchStateSyncPacketHandler>.Instance.Send(newStateType, H.Session.StateTimerConfig[newStateType], roundEndData);
        }

        // Everyone runs this when the match state packet is approved
        public void TransitionToState(MatchStateSyncPacket packet)
        {
            if (_currentState != null)
            {
                _currentState.OnExit();
                EventBus.OnEnd?.Invoke(_currentState.StateType);
            }

            PhaseDurationSeconds = H.Session.StateTimerConfig[packet.matchState];
            ServerPhaseStartSeconds = packet.serverPhaseStartSeconds;

            if (packet.roundActionEnd.HasValue)
            {
                LastRoundActionEnd = packet.roundActionEnd.Value;
                EventBus.OnRoundActionEnd?.Invoke(packet.roundActionEnd.Value);
            }

            session.matchState = packet.matchState;
            _currentState = ActiveRules.CreateState(packet.matchState);

            D.LogArenaController($"Entering {_currentState.GetType()} at {NetworkTime.ServerNowSeconds}");

            StateTimer = (float)(ServerPhaseStartSeconds + PhaseDurationSeconds - NetworkTime.ServerNowSeconds);

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

            int mvpId = MvpCalculator.CalculateRoundMvp(w, reason, H.Arena.LastObjectiveBombState, H.Arena.LastObjectivePlayerId);

            H.Arena.PendingRoundActionEnd = new RoundActionPhaseEnd { mvpId = mvpId, winner = w, roundWinReason = reason };
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
            Singleton<ArenaController>.Instance.Update();
            EventBus.OnUpdate?.Invoke();
        }

        private void FixedUpdate()
        {
            onUpdate?.Invoke();
            Singleton<ArenaController>.Instance.Update();
            EventBus.OnFixedUpdate?.Invoke();
        }

        private void LateUpdate()
        {
            onLateUpdate?.Invoke();
            EventBus.OnLateUpdate?.Invoke();
        }
    }
}