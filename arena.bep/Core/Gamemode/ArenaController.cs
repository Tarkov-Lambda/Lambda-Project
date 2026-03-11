using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using ifp.arena.bep;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.shared;
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
    }

    public struct RoundActionPhaseEnd
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

        public void StartSession(GameWorld gameWorld)
        {

            if (H.GameWorld is HideoutGameWorld) return;

            _tickerObject = new GameObject("Arena Gamesession");
            _tickerObject.AddComponent<GameModeTicker>();
            _tickerObject.AddComponent<TimeSyncTicker>();
            UnityEngine.Object.DontDestroyOnLoad(_tickerObject);


            PlayerUtils.ApplyPainkiller();
            //Singleton<AssetBundleHandler>.Instance.LoadMap("Lobby");

            H.Notify("Plugin Reloaded");
            if (session == null) session = new SessionInfo();
            if (FikaBackendUtils.IsClient)
            {
                Singleton<AdminLoginPacketHandler>.Instance.Send();
            }
        }

        public void EndSession(GameWorld gameWorld)
        {
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

            if (packet.hasRoundActionEnd)
            {
                LastRoundActionEnd = packet.roundActionEnd;
                EventBus.OnRoundActionEnd?.Invoke(packet.roundActionEnd);
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
            if (bombVisuals == null)
            {
                Singleton<ItemFactoryClass>.Instance.ItemTemplates.TryGetValue(SnDModeRules.bombTemplateId, out ItemTemplate itemTemplate);
                bombVisuals = Singleton<PoolManagerClass>.Instance.method_2(itemTemplate.Prefab, default); // Retrieve GameObject Mesh
            }

            if (bombStatePacket.state == BombState.Planted)
                bombVisuals.transform.position = bombStatePacket.position;

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

    public class GameModeTicker : MonoBehaviour
    {
        private List<BuyMenuEntry> _buyEntries;
        private float _nextBuyRefreshTime;

        private class BuyMenuEntry
        {
            public string name;
            public Item item;
            public int price;

        }

        private void RefreshBuyEntries(bool force)
        {
            if (!force && Time.unscaledTime < _nextBuyRefreshTime)
                return;

            _nextBuyRefreshTime = Time.unscaledTime + 1.0f; // refresh at most once a second

            _buyEntries = new List<BuyMenuEntry>();

            if (!Singleton<ItemFactoryClass>.Instantiated)
                return;

            // Dedup by weapon template id (you mentioned multiple builds may exist per gun)
            var builds = PresetUtils.Templates
                .Where(b => b?.Item != null)
                .GroupBy(b => b.Item.TemplateId)
                .Select(g => g.First());

            foreach (var b in builds)
            {
                var item = b.Item;
                if (item == null) continue;

                int price = Purchasing.GetItemPrice(item);
                if (price <= 0) continue;

                string name = !string.IsNullOrWhiteSpace(b.HandbookName)
                    ? b.HandbookName
                    : !string.IsNullOrWhiteSpace(item.ShortName)
                        ? item.ShortName
                        : item.Name;

                _buyEntries.Add(new BuyMenuEntry
                {
                    name = name,
                    item = item,
                    price = price
                });
            }

            Item tushonka = ItemsUtils.CreateItemFromTemplateId(SnDModeRules.bombTemplateId);

            _buyEntries.Add(new BuyMenuEntry { item = tushonka, name = tushonka.LocalizedName(), price = 2 });

            _buyEntries = _buyEntries.OrderBy(e => e.price).ThenBy(e => e.name).ToList();
        }

        private void Update()
        {
            if (Singleton<ArenaController>.Instantiated)
                Singleton<ArenaController>.Instance.Update();

        }
    }
}