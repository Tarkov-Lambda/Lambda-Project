using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
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

namespace ifp.arena.bep.GameTypes
{
    // ---------------------------------------------------------
    // CORE INTERFACES & ABSTRACTS (Zero Dependency Segment)
    // ---------------------------------------------------------
    public interface IGameState
    {
        RoundState StateType { get; }
        void OnEnter(BaseGameMode gameMode);
        RoundState? OnUpdate(BaseGameMode gameMode); // Returns next state, or null to stay
        void OnExit(BaseGameMode gameMode);
    }

    public abstract class GameModeRules
    {
        public abstract IGameState CreateState(RoundState state);
        public abstract void DrawTopBar(BaseGameMode game, Rect bounds, GUIStyle header, GUIStyle scoreBig, GUIStyle timer);

        // Base Scoreboard logic (Shared across modes by default)
        public virtual void DrawScoreboard(BaseGameMode game, Rect bounds, Texture2D bg, Texture2D highlight, GUIStyle header, GUIStyle row)
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

            foreach (var p in game.session.scoreboard.Values.OrderByDescending(p => p.kills))
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

                bool isWarmup = game.session.roundState == RoundState.Warmup;
                GUI.color = isWarmup ? (p.isReady ? Color.green : Color.yellow) : (p.isAlive ? Color.green : Color.red);
                GUI.Label(new Rect(bounds.x + 675f, currentY, 100, rowHeight), isWarmup ? (p.isReady ? "READY" : "WAITING") : (p.isAlive ? "ALIVE" : "DEAD"), row);

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

    // ---------------------------------------------------------
    // GAME MANAGER & TICKER
    // ---------------------------------------------------------
    public class BaseGameMode : Singleton<BaseGameMode>, IDisposable
    {
        public SessionInfo session;
        public GameModeRules ActiveRules { get; set; } = new SnDModeRules(); // Set to FFAModeRules() to switch mode

        public float StateTimer;
        public double ServerPhaseStartSeconds, PhaseDurationSeconds;

        private IGameState _currentState;
        private GameObject _tickerObject;

        public BaseGameMode()
        {
            if (Singleton<GameWorld>.Instance != null) StartSession(Singleton<GameWorld>.Instance);
            Patch_Gameworld_OnGameStarted.OnGameStarted += StartSession;
            Patch_Gameworld_OnDispose.OnDispose += EndSession;
        }

        public void Dispose()
        {
            Patch_Gameworld_OnGameStarted.OnGameStarted -= StartSession;
            Patch_Gameworld_OnDispose.OnDispose -= EndSession;
            EndSession(Singleton<GameWorld>.Instance);
            Release(this);
        }

        public async void StartSession(GameWorld gameWorld)
        {
            _tickerObject = new GameObject("SnD_GameModeTicker");
            _tickerObject.AddComponent<GameModeTicker>();
            _tickerObject.AddComponent<TimeSyncTicker>();
            UnityEngine.Object.DontDestroyOnLoad(_tickerObject);

            await Singleton<AssetBundleHandler>.Instance.LoadMap("Lobby");

            if (session == null) session = new SessionInfo();
        }

        public void EndSession(GameWorld gameWorld) { if (_tickerObject != null) UnityEngine.Object.Destroy(_tickerObject); }

        public void Update()
        {
            if (session == null || _currentState == null) return;

            if (FikaBackendUtils.IsServer) StateTimer -= Time.deltaTime;
            else StateTimer = (float)((ServerPhaseStartSeconds + PhaseDurationSeconds) - NetworkTime.ServerNowSeconds);

            RoundState? nextState = _currentState.OnUpdate(this);
            if (nextState.HasValue && FikaBackendUtils.IsServer)
                ChangeState(nextState.Value);
        }

        public void ChangeState(RoundState newStateType)
        {
            if (FikaBackendUtils.IsClient) return;
            _currentState?.OnExit(this);

            _currentState = ActiveRules.CreateState(newStateType);
            session.roundState = _currentState.StateType;
            _currentState.OnEnter(this);

            if (Singleton<AbstractGame>.Instance != null)
            {
                ServerPhaseStartSeconds = NetworkTime.ServerNowSeconds;
                PhaseDurationSeconds = StateTimer;
            }

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
            StateTimer = (float)((ServerPhaseStartSeconds + PhaseDurationSeconds) - NetworkTime.ServerNowSeconds);
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
            if (!Singleton<BaseGameMode>.Instantiated || Singleton<BaseGameMode>.Instance.session == null) return;
            if (!_stylesInitialized) InitStyles();

            var game = Singleton<BaseGameMode>.Instance;
            Rect topBarRect = new Rect((Screen.width / 2f) - 200f, 0, 400f, 60f);

            GUI.DrawTexture(topBarRect, _darkBackground);
            game.ActiveRules.DrawTopBar(game, topBarRect, _headerStyle, _scoreBigStyle, _timerStyle);

            if (Input.GetKey(KeyCode.Tab))
            {
                Rect sbBounds = new Rect((Screen.width - 800f) / 2f, (Screen.height - 500f) / 2f, 800f, 500f);
                game.ActiveRules.DrawScoreboard(game, sbBounds, _darkBackground, _rowHighlight, _headerStyle, _rowStyle);
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

        private void Update() { if (Singleton<BaseGameMode>.Instantiated) Singleton<BaseGameMode>.Instance.Update(); }
    }

    // ---------------------------------------------------------
    // SHARED STATES (SnD & FFA)
    // ---------------------------------------------------------
    public class SharedNone : IGameState
    {
        public RoundState StateType => RoundState.None;
        public void OnEnter(BaseGameMode game) { Teleporter.Teleport(Singleton<GameWorld>.Instance.MainPlayer); }
        public RoundState? OnUpdate(BaseGameMode game)
        {
            return null;
        }
        public void OnExit(BaseGameMode game) { }
    }

    public class SharedWarmup : IGameState
    {
        public RoundState StateType => RoundState.Warmup;
        public void OnEnter(BaseGameMode game) { if (FikaBackendUtils.IsServer) game.StateTimer = 45f; }
        public RoundState? OnUpdate(BaseGameMode game)
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (game.StateTimer <= 0 || (game.session.scoreboard.Count > 0 && game.session.scoreboard.Values.All(p => p.isReady)))
                return RoundState.WarmupEnd;
            return null;
        }
        public void OnExit(BaseGameMode game) { }
    }

    public class SharedWarmupEnd : IGameState
    {
        public RoundState StateType => RoundState.WarmupEnd;
        public void OnEnter(BaseGameMode game) { if (FikaBackendUtils.IsServer) game.StateTimer = 5f; }
        public RoundState? OnUpdate(BaseGameMode game) => FikaBackendUtils.IsServer && game.StateTimer <= 0 ? RoundState.Prepare : null;
        public void OnExit(BaseGameMode game) { }
    }

    public class SharedPrepare : IGameState
    {
        public RoundState StateType => RoundState.Prepare;
        public void OnEnter(BaseGameMode game)
        {
            foreach (var p in game.session.scoreboard.Values) p.isAlive = true;
            if (Singleton<GameWorld>.Instance?.MainPlayer != null)
            {
                Teleporter.Teleport(Singleton<GameWorld>.Instance.MainPlayer);
                Patch_Kill.FixMe(Singleton<GameWorld>.Instance.MainPlayer.ActiveHealthController);
            }
            if (FikaBackendUtils.IsServer) game.StateTimer = 5f;
            game.session.scoreboard.Clear();
            game.session.InitializeScoreBoard();
        }
        public RoundState? OnUpdate(BaseGameMode game) => FikaBackendUtils.IsServer && game.StateTimer <= 0 ? RoundState.Action : null;
        public void OnExit(BaseGameMode game) { }
    }

    public class SharedEnd : IGameState
    {
        public RoundState StateType => RoundState.End;
        public void OnEnter(BaseGameMode game) { if (FikaBackendUtils.IsServer) { game.StateTimer = 10f; game.OnRoundEnd(); } }
        public RoundState? OnUpdate(BaseGameMode game) => FikaBackendUtils.IsServer && game.StateTimer <= 0 ? RoundState.Prepare : null;
        public void OnExit(BaseGameMode game) { }
    }

    // ---------------------------------------------------------
    // S&D IMPLEMENTATION
    // ---------------------------------------------------------
    public class SnDAction : IGameState
    {
        public RoundState StateType => RoundState.Action;
        public void OnEnter(BaseGameMode game) { if (FikaBackendUtils.IsServer) game.StateTimer = 120f; }
        public RoundState? OnUpdate(BaseGameMode game)
        {
            if (!FikaBackendUtils.IsServer) return null;
            Faction? winner = CheckWipe(game);
            if (winner.HasValue) { Award(game, winner.Value); return RoundState.End; }
            if (game.session.bombState == BombState.Planted) return RoundState.Planted;
            if (game.StateTimer <= 0) { Award(game, Faction.CT); return RoundState.End; }
            return null;
        }
        public void OnExit(BaseGameMode game) { }

        private Faction? CheckWipe(BaseGameMode game)
        {
            var alive = game.session.scoreboard.Values.Where(p => p.isAlive).GroupBy(p => p.faction).ToDictionary(g => g.Key, g => g.Count());
            var factions = game.session.scoreboard.Values.Select(p => p.faction).Where(f => f != Faction.None).Distinct();
            foreach (var f in factions) if (!alive.ContainsKey(f) || alive[f] == 0) return factions.FirstOrDefault(o => o != f);
            return null;
        }
        private void Award(BaseGameMode game, Faction w) { if (!game.session.factionWins.ContainsKey(w)) game.session.factionWins[w] = 0; game.session.factionWins[w]++; }
    }

    public class SnDPlanted : IGameState
    {
        public RoundState StateType => RoundState.Planted;
        public void OnEnter(BaseGameMode game) { if (FikaBackendUtils.IsServer) game.StateTimer = 45f; }
        public RoundState? OnUpdate(BaseGameMode game)
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (!game.session.scoreboard.Values.Any(p => p.isAlive && p.faction == Faction.CT)) { Award(game, Faction.T); return RoundState.End; }
            if (game.StateTimer <= 0) { Award(game, Faction.T); return RoundState.End; }
            return null;
        }
        public void OnExit(BaseGameMode game) { }
        private void Award(BaseGameMode game, Faction w) { if (!game.session.factionWins.ContainsKey(w)) game.session.factionWins[w] = 0; game.session.factionWins[w]++; }
    }

    public class SnDModeRules : GameModeRules
    {
        public override IGameState CreateState(RoundState state) => state switch
        {
            RoundState.Warmup => new SharedWarmup(),
            RoundState.WarmupEnd => new SharedWarmupEnd(),
            RoundState.Prepare => new SharedPrepare(),
            RoundState.Action => new SnDAction(),
            RoundState.Planted => new SnDPlanted(),
            RoundState.End => new SharedEnd(),
            _ => null
        };

        public override void DrawTopBar(BaseGameMode game, Rect bounds, GUIStyle header, GUIStyle scoreBig, GUIStyle timer)
        {
            GUI.Label(new Rect(bounds.x, bounds.y, 100, bounds.height - 20), "T", header);
            GUI.Label(new Rect(bounds.x, bounds.y + 15, 100, bounds.height), game.session.factionWins.GetValueOrDefault(Faction.T, 0).ToString(), scoreBig);
            GUI.Label(new Rect(bounds.x + bounds.width - 100, bounds.y, 100, bounds.height - 20), "CT", header);
            GUI.Label(new Rect(bounds.x + bounds.width - 100, bounds.y + 15, 100, bounds.height), game.session.factionWins.GetValueOrDefault(Faction.CT, 0).ToString(), scoreBig);
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 50, 5, 100, bounds.height), FormatTime(game.StateTimer), timer);
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 50, 40, 100, 20), game.session.roundState.ToString().ToUpper(), header);
        }
    }

    // ---------------------------------------------------------
    // FFA IMPLEMENTATION
    // ---------------------------------------------------------
    public class FFAAction : IGameState
    {
        public RoundState StateType => RoundState.Action;
        public void OnEnter(BaseGameMode game) { if (FikaBackendUtils.IsServer) game.StateTimer = 600f; } // 10 min
        public RoundState? OnUpdate(BaseGameMode game)
        {
            if (!FikaBackendUtils.IsServer) return null;
            Plugin.Logger.LogInfo(game.session.scoreboard.Values.Any(p => p.kills >= 1));
            if (game.StateTimer <= 0 || game.session.scoreboard.Values.Any(p => p.kills >= 3)) return RoundState.End;
            return null;
        }
        public void OnExit(BaseGameMode game) { }
    }

    public class FFAModeRules : GameModeRules
    {
        public override IGameState CreateState(RoundState state) => state switch
        {
            RoundState.Warmup => new SharedWarmup(),
            RoundState.WarmupEnd => new SharedWarmupEnd(),
            RoundState.Prepare => new SharedPrepare(),
            RoundState.Action => new FFAAction(),
            RoundState.End => new SharedEnd(),
            _ => null
        };

        public override void DrawTopBar(BaseGameMode game, Rect bounds, GUIStyle header, GUIStyle scoreBig, GUIStyle timer)
        {
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 50, 5, 100, bounds.height), FormatTime(game.StateTimer), timer);
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 50, 40, 100, 20), "FFA", header);

            var top = game.session.scoreboard.Values.OrderByDescending(p => p.kills).Take(2).ToList();
            if (top.Count > 0)
            {
                GUI.Label(new Rect(bounds.x, bounds.y, 100, 20), "1ST", header);
                GUI.Label(new Rect(bounds.x, bounds.y + 15, 100, bounds.height), top[0].kills.ToString(), scoreBig);
            }
            if (top.Count > 1)
            {
                GUI.Label(new Rect(bounds.x + bounds.width - 100, bounds.y, 100, 20), "2ND", header);
                GUI.Label(new Rect(bounds.x + bounds.width - 100, bounds.y + 15, 100, bounds.height), top[1].kills.ToString(), scoreBig);
            }
        }
    }
}