using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using ifp.arena.bep.Core.Audio;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.bep.Patches.Tarkov;
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
        public abstract void DrawTopBar(ArenaController game, Rect bounds, GUIStyle header, GUIStyle scoreBig, GUIStyle timer);

        // Base Scoreboard logic (Shared across modes by default)
        public virtual void DrawScoreboard(ArenaController game, Rect bounds, Texture2D bg, Texture2D highlight, GUIStyle header, GUIStyle row)
        {
            GUI.DrawTexture(bounds, bg);
            float currentY = bounds.y + 20f, rowHeight = 35f;

            GUI.Label(new Rect(bounds.x + 20f, currentY, 200, rowHeight), "PLAYER", header);
            GUI.Label(new Rect(bounds.x + 300f, currentY, 100, rowHeight), "FACTION", header);
            GUI.Label(new Rect(bounds.x + 450f, currentY, 50, rowHeight), "K", header);
            GUI.Label(new Rect(bounds.x + 525f, currentY, 50, rowHeight), "D", header);
            GUI.Label(new Rect(bounds.x + 600f, currentY, 50, rowHeight), "MONEY", header);
            GUI.Label(new Rect(bounds.x + 675f, currentY, 100, rowHeight), "STATUS", header);
            currentY += 40f;

            foreach (var p in H.Arena.session.scoreboard.Values.OrderByDescending(p => p.kills))
            {
                Rect rowRect = new Rect(bounds.x, currentY, bounds.width, rowHeight);
                if (!p.isAlive) { GUI.color = new Color(1f, 0.5f, 0.5f, 0.3f); GUI.DrawTexture(rowRect, highlight); }
                else if (p.player != null && H.GameWorld.MainPlayer != null && p.player.Id == H.GameWorld.MainPlayer.Id)
                { GUI.color = new Color(1f, 1f, 1f, 0.1f); GUI.DrawTexture(rowRect, highlight); }

                GUI.color = p.isAlive ? Color.white : Color.gray;
                GUI.Label(new Rect(bounds.x + 20f, currentY, 250, rowHeight), p.player?.Profile?.Nickname ?? "Connecting...", row);
                GUI.Label(new Rect(bounds.x + 300f, currentY, 100, rowHeight), p.faction.ToString(), row);
                GUI.Label(new Rect(bounds.x + 450f, currentY, 50, rowHeight), p.kills.ToString(), row);
                GUI.Label(new Rect(bounds.x + 525f, currentY, 50, rowHeight), p.deaths.ToString(), row);
                // GUI.Label(new Rect(bounds.x + 600f, currentY, 50, rowHeight), p.assists.ToString(), row);
                GUI.Label(new Rect(bounds.x + 600f, currentY, 50, rowHeight), p.money.ToString(), row);

                bool isWarmup = H.Arena.session.roundState == MatchState.Warmup;
                GUI.color = isWarmup ? p.isReady ? Color.green : Color.yellow : p.isAlive ? Color.green : Color.red;
                GUI.Label(new Rect(bounds.x + 675f, currentY, 100, rowHeight), isWarmup ? p.isReady ? "READY" : "WAITING" : p.isAlive ? "ALIVE" : "DEAD", row);

                GUI.color = Color.white;
                currentY += rowHeight;
            }
        }

        protected string FormatTime(float seconds)
        {
            if (seconds < 0) seconds = 0;
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return $"{t.Minutes:D2}:{t.Seconds:D2}";
        }
    }

    public static class EventBus
    {
        public static Action<MatchState> OnEnter;
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

            _musicObject = new GameObject("ArenaMusicKit");
            _musicObject.AddComponent<MusicManager>();
            _musicObject.AddComponent<MusicEventRouter>();
            _musicObject.transform.SetParent(H.MainPlayer.PlayerBody.transform, false);

            H.PlayMusic(MusicEvent.DeathCam);

            PlayerUtils.ApplyPainkiller();
            PlayerUtils.RegisterAllBullets();
            //Singleton<AssetBundleHandler>.Instance.LoadMap("Lobby");

            H.Notify("Plugin Reloaded");
            if (session == null) session = new SessionInfo();
            if (FikaBackendUtils.IsClient)
            {
                // Singleton<AdminLoginPacketHandler>.Instance.Send();
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

            // Unified Timer Logic (Works for both Server and Client)
            // On Server: Start + Duration - Now ~= Duration (since Start is Now)
            // On Client: Start + Duration - Now = Remaining Time accurately synced
            StateTimer = (float)(ServerPhaseStartSeconds + PhaseDurationSeconds - NetworkTime.ServerNowSeconds);

            // Only Server runs the logic loop
            if (FikaBackendUtils.IsServer)
            {
                MatchState? nextState = _currentState.OnUpdate();
                if (nextState.HasValue)
                {
                    ChangeState(nextState.Value);
                }
            }
        }

        public void ChangeState(MatchState newStateType)
        {
            RoundActionPhaseEnd? roundEndData = PendingRoundActionEnd;
            PendingRoundActionEnd = null;

            Singleton<MatchStateSyncPacketHandler>.Instance.Send(newStateType, H.Session.StateTimerConfig[newStateType], roundEndData);
        }

        public void TransitionToState(MatchStateSyncPacket packet)
        {
            if (_currentState != null)
            {
                _currentState.OnExit();
                EventBus.OnEnd?.Invoke(_currentState.StateType);
            }

            PhaseDurationSeconds = packet.phaseDurationSeconds;
            ServerPhaseStartSeconds = packet.serverPhaseStartSeconds;

            if (packet.hasRoundActionEnd)
            {
                LastRoundActionEnd = packet.roundActionEnd;
                EventBus.OnRoundActionEnd?.Invoke(packet.roundActionEnd);
            }

            session.roundState = packet.roundState;
            _currentState = ActiveRules.CreateState(packet.roundState);

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
                bombVisuals = Singleton<PoolManagerClass>.Instance.method_2(itemTemplate.Prefab, default);
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
                Singleton<Effects>.Instance.Emit("Gas_explosion", explosionCenter, Vector3.up * 3f);
            }
        }

        public void OnRoundEnd() => Singleton<SessionInfoPacketHandler>.Instance.Send();
    }


    public class GameModeTicker : MonoBehaviour
    {
        private GUIStyle _headerStyle, _rowStyle, _timerStyle, _scoreBigStyle;
        private GUIStyle _mvpStyle;
        private GUIStyle _buyHeaderStyle, _buyRowStyle;
        private Texture2D _darkBackground, _rowHighlight;
        private bool _stylesInitialized = false;

        private bool _showBuyMenu;
        private Vector2 _buyScroll;
        private List<BuyMenuEntry> _buyEntries;
        private float _nextBuyRefreshTime;

        private class BuyMenuEntry
        {
            public string name;
            public Item item;
            public int price;

        }

        private void OnGUI()
        {
            if (!Singleton<ArenaController>.Instantiated || Singleton<ArenaController>.Instance.session == null) return;
            if (!_stylesInitialized) InitStyles();

            var game = Singleton<ArenaController>.Instance;
            Rect topBarRect = new Rect(Screen.width / 2f - 200f, 0, 400f, 60f);

            GUI.DrawTexture(topBarRect, _darkBackground);
            H.Arena.ActiveRules.DrawTopBar(game, topBarRect, _headerStyle, _scoreBigStyle, _timerStyle);

            if (Input.GetKey(KeyCode.Tab))
            {
                Rect sbBounds = new Rect((Screen.width - 800f) / 2f, (Screen.height - 500f) / 2f, 800f, 500f);
                H.Arena.ActiveRules.DrawScoreboard(game, sbBounds, _darkBackground, _rowHighlight, _headerStyle, _rowStyle);
            }

            DrawBuyMenu(game);

            DrawRoundMvpBanner(game);
        }

        private void HandleBuyMenuToggle(ArenaController game)
        {
            // Only process toggle once per-frame in Update() (OnGUI can be called multiple times)
            if (game == null || game.session == null) return;

            bool isBuyTime = game.session.roundState == MatchState.RoundPrepare;
            if (!isBuyTime)
            {
                _showBuyMenu = false;
                return;
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                _showBuyMenu = !_showBuyMenu;
                if (_showBuyMenu)
                    RefreshBuyEntries(force: true);
            }
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

            Item tushonka = PresetUtils.CreateItem(SnDModeRules.bombTemplateId);

            _buyEntries.Add(new BuyMenuEntry { item = tushonka, name = tushonka.LocalizedName(), price = 2 });

            _buyEntries = _buyEntries.OrderBy(e => e.price).ThenBy(e => e.name).ToList();
        }

        private void DrawBuyMenu(ArenaController game)
        {
            if (!_showBuyMenu) return;
            // if (game.session.roundState != MatchState.RoundPrepare) return;

            RefreshBuyEntries(force: false);

            float width = 340f;
            float height = 520f;
            Rect panel = new Rect(20f, 80f, width, height);

            GUI.DrawTexture(panel, _darkBackground);

            Rect header = new Rect(panel.x, panel.y, panel.width, 40f);
            GUI.Label(header, "BUY MENU", _buyHeaderStyle);

            var myScore = H.MainPlayer != null ? H.GetPlayerScore(H.MainPlayer.Id) : null;
            int money = myScore?.money ?? 0;
            GUI.Label(new Rect(panel.x + 12f, panel.y + 42f, panel.width - 24f, 22f), $"${money}", _buyRowStyle);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 62f, panel.width - 24f, 18f), "(B to close)", _rowStyle);

            Rect listArea = new Rect(panel.x + 10f, panel.y + 85f, panel.width - 20f, panel.height - 95f);
            GUI.BeginGroup(listArea);

            float innerWidth = listArea.width - 16f;
            float contentHeight = (_buyEntries?.Count ?? 0) * 30f + 10f;
            Rect viewRect = new Rect(0, 0, innerWidth, contentHeight);
            Rect scrollRect = new Rect(0, 0, listArea.width, listArea.height);
            _buyScroll = GUI.BeginScrollView(scrollRect, _buyScroll, viewRect);


            if (_buyEntries == null || _buyEntries.Count == 0)
            {
                GUI.Label(new Rect(0, 0, innerWidth, 24f), "No weapon presets found.", _rowStyle);
            }
            else
            {
                float y = 0f;
                foreach (var entry in _buyEntries)
                {
                    bool canAfford = money >= entry.price;
                    string label = $"{entry.name}   (${entry.price})";

                    var prevEnabled = GUI.enabled;
                    GUI.enabled = canAfford;

                    if (GUI.Button(new Rect(0, y, innerWidth, 26f), label))
                    {
                        PresetUtils.SpawnItem(entry.item);
                    }

                    GUI.enabled = prevEnabled;
                    y += 30f;
                }
            }

            GUI.EndScrollView();
            GUI.EndGroup();
        }

        private void InitStyles()
        {
            _darkBackground = MakeTex(2, 2, new Color(0, 0, 0, 0.85f));
            _rowHighlight = MakeTex(2, 2, new Color(1, 1, 1, 1f));
            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _headerStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            _rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            _rowStyle.normal.textColor = Color.white;
            _timerStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _timerStyle.normal.textColor = new Color(1f, 0.8f, 0.2f);
            _scoreBigStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _scoreBigStyle.normal.textColor = Color.white;
            _mvpStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _mvpStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

            _buyHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _buyHeaderStyle.normal.textColor = Color.white;
            _buyRowStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _buyRowStyle.normal.textColor = new Color(0.3f, 1f, 0.3f);
            _stylesInitialized = true;
        }

        private void DrawRoundMvpBanner(ArenaController game)
        {
            if (game.session.roundState != MatchState.RoundEnd) return;

            var payload = game.LastRoundActionEnd;
            if (!payload.HasValue) return;

            string text;
            if (payload.Value.mvpId <= 0)
            {
                text = "NO MVP AWARDED";
            }
            else
            {
                var ps = H.GetPlayerScore(payload.Value.mvpId);
                string name = ps?.player?.Profile?.Nickname ?? $"Player {payload.Value.mvpId}";
                text = $"ROUND MVP: {name}";
            }

            Rect bounds = new Rect(Screen.width / 2f - 250f, 70f, 500f, 28f);
            GUI.DrawTexture(new Rect(bounds.x, bounds.y, bounds.width, bounds.height), _darkBackground);
            GUI.Label(bounds, text, _mvpStyle);
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h]; for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(w, h); result.SetPixels(pix); result.Apply(); return result;
        }

        private void Update()
        {
            if (Singleton<ArenaController>.Instantiated)
                Singleton<ArenaController>.Instance.Update();

            // Input handling here so GetKeyDown is reliable
            if (Singleton<ArenaController>.Instantiated)
                HandleBuyMenuToggle(Singleton<ArenaController>.Instance);
        }
    }
}