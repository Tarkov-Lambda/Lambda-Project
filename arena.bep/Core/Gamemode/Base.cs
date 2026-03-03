using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.bep.Patches.Fika;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using UnityEngine;

namespace ifp.arena.bep.Core.Gamemode
{
    // ---------------------------------------------------------
    // CORE INTERFACES & ABSTRACTS (Zero Dependency Segment)
    // ---------------------------------------------------------
    public interface IGameState
    {
        RoundState StateType { get; }
        void OnEnter();
        RoundState? OnUpdate(); // Returns next state, or null to stay
        void OnExit();
    }

    public abstract class GameModeRules
    {
        public abstract IGameState CreateState(RoundState state);
        public abstract void DrawTopBar(Base game, Rect bounds, GUIStyle header, GUIStyle scoreBig, GUIStyle timer);

        // Base Scoreboard logic (Shared across modes by default)
        public virtual void DrawScoreboard(Base game, Rect bounds, Texture2D bg, Texture2D highlight, GUIStyle header, GUIStyle row)
        {
            GUI.DrawTexture(bounds, bg);
            float currentY = bounds.y + 20f, rowHeight = 35f;

            GUI.Label(new Rect(bounds.x + 20f, currentY, 200, rowHeight), "PLAYER", header);
            GUI.Label(new Rect(bounds.x + 300f, currentY, 100, rowHeight), "FACTION", header);
            GUI.Label(new Rect(bounds.x + 450f, currentY, 50, rowHeight), "K", header);
            GUI.Label(new Rect(bounds.x + 525f, currentY, 50, rowHeight), "D", header);
            GUI.Label(new Rect(bounds.x + 600f, currentY, 50, rowHeight), "A", header);
            GUI.Label(new Rect(bounds.x + 675f, currentY, 100, rowHeight), "STATUS", header);
            currentY += 40f;

            foreach (var p in H.game.session.scoreboard.Values.OrderByDescending(p => p.kills))
            {
                Rect rowRect = new Rect(bounds.x, currentY, bounds.width, rowHeight);
                if (!p.isAlive) { GUI.color = new Color(1f, 0.5f, 0.5f, 0.3f); GUI.DrawTexture(rowRect, highlight); }
                else if (p.player != null && Singleton<GameWorld>.Instance.MainPlayer != null && p.player.Id == Singleton<GameWorld>.Instance.MainPlayer.Id)
                { GUI.color = new Color(1f, 1f, 1f, 0.1f); GUI.DrawTexture(rowRect, highlight); }

                GUI.color = p.isAlive ? Color.white : Color.gray;
                GUI.Label(new Rect(bounds.x + 20f, currentY, 250, rowHeight), p.player?.Profile?.Nickname ?? "Connecting...", row);
                GUI.Label(new Rect(bounds.x + 300f, currentY, 100, rowHeight), p.faction.ToString(), row);
                GUI.Label(new Rect(bounds.x + 450f, currentY, 50, rowHeight), p.kills.ToString(), row);
                GUI.Label(new Rect(bounds.x + 525f, currentY, 50, rowHeight), p.deaths.ToString(), row);
                GUI.Label(new Rect(bounds.x + 600f, currentY, 50, rowHeight), p.assists.ToString(), row);

                bool isWarmup = H.game.session.roundState == RoundState.Warmup;
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

    public static class EventBus {
        static Action<RoundState> OnEnter;
        static Action<RoundState> OnEnd;
        
        static Action<BombState> OnBombStateChange;

        static Action<Player> OnPlayerKill;
        static Action<Player> OnPlayerDeath;
    }
    
    // This is the place where we manage both server/client arena behaviour
    public class Base : Singleton<Base>, IDisposable
    {
        public SessionInfo session;
        public GameModeRules ActiveRules { get; set; } = new SnDModeRules();

        public float StateTimer;
        public double ServerPhaseStartSeconds, PhaseDurationSeconds;

        private IGameState _currentState;
        private GameObject _tickerObject;

        public Base()
        {
            if (H.gameWorld != null) StartSession(H.gameWorld);
            Patch_Gameworld_OnGameStarted.OnGameStarted += StartSession;
            Patch_Gameworld_OnDispose.OnDispose += EndSession;
        }

        public void Dispose()
        {
            Patch_Gameworld_OnGameStarted.OnGameStarted -= StartSession;
            Patch_Gameworld_OnDispose.OnDispose -= EndSession;
            EndSession(H.gameWorld);
            Release(this);
        }

        public void StartSession(GameWorld gameWorld)
        {
            _tickerObject = new GameObject("Arena Gamesession");
            _tickerObject.AddComponent<GameModeTicker>();
            _tickerObject.AddComponent<TimeSyncTicker>();
            UnityEngine.Object.DontDestroyOnLoad(_tickerObject);

            //Singleton<AssetBundleHandler>.Instance.LoadMap("Lobby");

            if (session == null) session = new SessionInfo();
        }

        public void EndSession(GameWorld gameWorld) {
            if (_tickerObject != null)
                UnityEngine.Object.Destroy(_tickerObject);
        }

        public void Update()
        {
            if (session == null || _currentState == null) return;

            // Timer Synchronization
            if (FikaBackendUtils.IsServer) StateTimer -= Time.deltaTime;
            else StateTimer = (float)(ServerPhaseStartSeconds + PhaseDurationSeconds - NetworkTime.ServerNowSeconds);

            RoundState? nextState = _currentState.OnUpdate();
            if (nextState.HasValue && FikaBackendUtils.IsServer)
                ChangeState(nextState.Value);
        }

        public void ChangeState(RoundState newStateType)
        {
            if (FikaBackendUtils.IsClient) return;
            _currentState?.OnExit();

            _currentState = ActiveRules.CreateState(newStateType);
            session.roundState = _currentState.StateType;
            _currentState.OnEnter();

            ServerPhaseStartSeconds = NetworkTime.ServerNowSeconds;
            PhaseDurationSeconds = StateTimer;

            if (FikaBackendUtils.IsServer)
                Singleton<RoundStateSyncPacketHandler>.Instance.Send(_currentState.StateType, StateTimer);
        }

        public void ApplyReplicatedRoundState(RoundState state, double phaseDurationSeconds, double serverPhaseStartSeconds)
        {
            PhaseDurationSeconds = phaseDurationSeconds;
            ServerPhaseStartSeconds = serverPhaseStartSeconds;
            NetworkTime.BootstrapFromServerStamp(serverPhaseStartSeconds);

            session.roundState = state;
            IGameState newState = ActiveRules.CreateState(state);
            if (newState == null) return;
            _currentState?.OnExit(this);
            _currentState = newState;
            StateTimer = (float)(ServerPhaseStartSeconds + PhaseDurationSeconds - NetworkTime.ServerNowSeconds);
            _currentState.OnEnter(this);
        }

        public void OnRoundEnd() => Singleton<SessionInfoPacketHandler>.Instance.Send();
    }

    public class GameModeTicker : MonoBehaviour
    {
        private GUIStyle _headerStyle, _rowStyle, _timerStyle, _scoreBigStyle;
        private Texture2D _darkBackground, _rowHighlight;
        private bool _stylesInitialized = false;

        private void OnGUI()
        {
            if (!Singleton<Base>.Instantiated || Singleton<Base>.Instance.session == null) return;
            if (!_stylesInitialized) InitStyles();

            var game = Singleton<Base>.Instance;
            Rect topBarRect = new Rect(Screen.width / 2f - 200f, 0, 400f, 60f);

            GUI.DrawTexture(topBarRect, _darkBackground);
            H.game.ActiveRules.DrawTopBar(game, topBarRect, _headerStyle, _scoreBigStyle, _timerStyle);

            if (Input.GetKey(KeyCode.Tab))
            {
                Rect sbBounds = new Rect((Screen.width - 800f) / 2f, (Screen.height - 500f) / 2f, 800f, 500f);
                H.game.ActiveRules.DrawScoreboard(game, sbBounds, _darkBackground, _rowHighlight, _headerStyle, _rowStyle);
            }
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
            _stylesInitialized = true;
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h]; for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(w, h); result.SetPixels(pix); result.Apply(); return result;
        }

        private void Update() {
            if (Singleton<Base>.Instantiated)
                Singleton<Base>.Instance.Update();
        }
    }
}