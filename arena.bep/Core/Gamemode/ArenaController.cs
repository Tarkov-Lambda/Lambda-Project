using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using ifp.arena.bep.Core.Audio;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using System;
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


        public ArenaController()
        {
            if (H.GameWorld != null) StartSession(H.GameWorld);
            Patch_Gameworld_OnGameStarted.OnGameStarted += StartSession;
            Patch_Gameworld_OnDispose.OnDispose += EndSession;
        }

        public void Dispose()
        {
            Patch_Gameworld_OnGameStarted.OnGameStarted -= StartSession;
            Patch_Gameworld_OnDispose.OnDispose -= EndSession;
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

        public void OnRoundEnd() => Singleton<SessionInfoPacketHandler>.Instance.Send();
    }


    public class GameModeTicker : MonoBehaviour
    {
        private GUIStyle _headerStyle, _rowStyle, _timerStyle, _scoreBigStyle;
        private GUIStyle _mvpStyle;
        private Texture2D _darkBackground, _rowHighlight;
        private bool _stylesInitialized = false;

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

            DrawRoundMvpBanner(game);
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
        }
    }
}