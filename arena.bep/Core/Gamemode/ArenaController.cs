using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.CameraControl;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using ifp.arena.bep;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.Ladders;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.shared;
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Effects;
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

        public static Action<RoundActionPhaseEnd> OnRoundActionEnd;
        public static Action<int> OnSelfMoneyAdded;

        public static Action OnItemBuy;
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

        private GameObject bombVisuals;
        public Vector3 BombPlantedPosition { get; private set; }

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

            ItemsUtils.ResetInventoryLock();

            _tickerObject = new GameObject("Arena Gamesession");
            _tickerObject.AddComponent<GameModeTicker>();
            _tickerObject.AddComponent<TimeSyncTicker>();
            UnityEngine.Object.DontDestroyOnLoad(_tickerObject);

            PlayerUtils.ApplyPainkiller();

            H.Notify("Plugin Reloaded");
            if (session == null) session = new SessionInfo();

            // Preloading bomb asset
            InitBombVisualsAsync().Forget();

            await Singleton<AssetBundleHandler>.Instance.LoadMap("lobby");
            Teleporter.Teleport(H.MainPlayer, "lobby");

            // delay is stupid
            if (FikaBackendUtils.IsClient)
            {
                await UniTask.Delay(200);
                Singleton<AdminLoginPacketHandler>.Instance.Send();
            }

        }

        private async UniTaskVoid InitBombVisualsAsync()
        {
            Item bombItem = ItemsUtils.CreateItemFromTemplateId(SnDModeRules.bombTemplateId);
            await ItemsUtils.LoadBundlesForItem(bombItem);
            bombVisuals = Singleton<PoolManagerClass>.Instance.CreateLootPrefab(bombItem, ECameraType.Default);
            bombVisuals.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(bombVisuals);
        }

        public void EndSession(GameWorld gameWorld)
        {
            // Cancel any in-flight ClientRequestGiveItem calls so they don't touch
            // inventory after the session has been torn down
            ItemsUtils.ResetInventoryLock();

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

            session.roundState = packet.matchState;
            _currentState = ActiveRules.CreateState(packet.matchState);

            StateTimer = (float)(ServerPhaseStartSeconds + PhaseDurationSeconds - NetworkTime.ServerNowSeconds);

            if (_currentState != null)
            {
                _currentState.OnEnter();
                EventBus.OnEnter?.Invoke(_currentState.StateType);
            }
        }

        public void SetBombVisuals(BombStatePacket bombStatePacket)
        {
            if (bombStatePacket.state == BombState.Planted)
            {
                BombPlantedPosition = bombStatePacket.position;
                bombVisuals.transform.position = bombStatePacket.position;
            }

            switch (bombStatePacket.state)
            {
                case BombState.Defusing:
                case BombState.Defused:
                case BombState.Planted:
                    bombVisuals.SetActive(true);
                    break;
                default:
                    bombVisuals.SetActive(false);
                    break;
            }

            if (bombStatePacket.state == BombState.Exploded)
            {
                Vector3 explosionCenter = bombVisuals.transform.position;
                float distance = Vector3.Distance(explosionCenter, H.MainPlayer.PlayerBody.transform.position);
                H.Log(distance.ToString());
                if (distance <= 25f)
                {
                    H.MainPlayer.ActiveHealthController.Kill(EDamageType.Explosion);
                }
                Singleton<Effects>.Instance.Emit("Gas_explosion", explosionCenter, Vector3.up * 0.1f);
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

    // legacy ig will refactor later
    public class GameModeTicker : MonoBehaviour
    {
        private void Update()
        {
            Singleton<ArenaController>.Instance.Update();
        }
    }
}