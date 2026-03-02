using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Modding.Events;
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
using UnityEngine;
using UnityEngine.UIElements.UIR;

namespace ifp.arena.bep.GameTypes
{
    public interface IGameState
    {
        RoundState StateType { get; }
        void OnEnter(BaseGameMode gameMode);
        void OnUpdate(BaseGameMode gameMode);
        void OnExit(BaseGameMode gameMode);
    }

    public class BaseGameMode : Singleton<BaseGameMode>, IDisposable
    {
        public SessionInfo session;
        public float StateTimer;

        // Used client-side to derive timer from server timestamps.
        public double ServerPhaseStartSeconds;
        public double PhaseDurationSeconds;

        private IGameState _currentState;
        private GameObject _tickerObject;

        public BaseGameMode()
        {
            // if statement for hot reloading
            if (Singleton<GameWorld>.Instance != null)
            {
                StartSession(Singleton<GameWorld>.Instance);
            }
            Patch_Gameworld_OnGameStarted.OnGameStarted += StartSession;
            Patch_Gameworld_OnDispose.OnDispose += EndSession;

            // Create a hidden GameObject to run Unity's Update loop for our State Machine
        }

        public void Dispose()
        {
            Patch_Gameworld_OnGameStarted.OnGameStarted -= StartSession;
            Patch_Gameworld_OnDispose.OnDispose -= EndSession;

            EndSession(Singleton<GameWorld>.Instance);

            Release(this);
        }

        public void StartSession(GameWorld gameWorld)
        {
            Plugin.Logger.LogInfo("asdsa");
            _tickerObject = new GameObject("SnD_GameModeTicker");
            _tickerObject.AddComponent<GameModeTicker>();
            _tickerObject.AddComponent<TimeSyncTicker>();
            UnityEngine.Object.DontDestroyOnLoad(_tickerObject);

            if (session != null) return;

            session = new SessionInfo();

            // Singleton<RestartPacketHandler>.Instance.Send();
        }

        public void EndSession(GameWorld gameWorld)
        {
            if (_tickerObject != null)
            {
                UnityEngine.Object.Destroy(_tickerObject);
            }
        }

        // Called by the GameModeTicker every Unity frame
        public void Update()
        {
            // Both client and server should tick. Server advances the authoritative state machine.
            // Clients derive remaining time from server timestamps and run local-only state effects.
            if (session == null) return;

            if (_currentState != null)
            {
                // If this instance is authoritative, keep a simple local countdown.
                if (FikaBackendUtils.IsServer)
                {
                    StateTimer -= Time.deltaTime;
                }
                else
                {
                    // Client derives remaining time from server timestamps.
                    double now = NetworkTime.ServerNowSeconds;
                    double end = ServerPhaseStartSeconds + PhaseDurationSeconds;
                    StateTimer = (float)(end - now);

                    // NotificationManagerClass.DisplayMessageNotification($"{now} {end} {StateTimer}");

                }

                _currentState.OnUpdate(this);
            }
        }

        public void ChangeState(IGameState newState)
        {
            // Client changes state only when server says so
            // Also avoiding double function execution here
            if (FikaBackendUtils.IsClient) return;

            if (_currentState != null)
            {
                _currentState.OnExit(this);
            }

            _currentState = newState;

            // Authoritative session state
            session.roundState = _currentState.StateType;

            // Enter sets the duration (StateTimer) and runs any side effects.
            _currentState.OnEnter(this);

            // Capture authoritative phase start for host-side UI as well.
            if (Singleton<AbstractGame>.Instance != null)
            {
                ServerPhaseStartSeconds = NetworkTime.ServerNowSeconds;
                PhaseDurationSeconds = StateTimer;
            }

            // Server replicates the new state to clients (with timestamps) so they can run local logic.
            if (FikaBackendUtils.IsServer)
            {
                Singleton<RoundStateSyncPacketHandler>.Instance.Send(_currentState.StateType, StateTimer);
            }
        }

        /// <summary>
        /// Client/server entry point for applying an authoritative state transition received over the network.
        /// This lets packet handlers remain data-only.
        /// </summary>
        public void ApplyReplicatedRoundState(RoundState state, double phaseDurationSeconds, double serverPhaseStartSeconds)
        {
            // Update replicated timer model
            PhaseDurationSeconds = phaseDurationSeconds;
            ServerPhaseStartSeconds = serverPhaseStartSeconds;

            // Bootstrap clock from the authoritative timestamp if we don't have sync yet.
            NetworkTime.BootstrapFromServerStamp(serverPhaseStartSeconds);
            session.roundState = state;

            // Swap local state object to match
            IGameState newState = state switch
            {
                RoundState.Warmup => new StateWarmup(),
                RoundState.WarmupEnd => new StateWarmupEnd(),
                RoundState.Prepare => new StatePrepare(),
                RoundState.Action => new StateAction(),
                RoundState.Planted => new StatePlanted(),
                RoundState.End => new StateEnd(),
                _ => null
            };

            if (newState == null) return;

            if (_currentState != null)
                _currentState.OnExit(this);

            _currentState = newState;

            // Derive remaining timer immediately
            double now = NetworkTime.ServerNowSeconds;
            double end = ServerPhaseStartSeconds + PhaseDurationSeconds;
            StateTimer = (float)(end - now);

            _currentState.OnEnter(this);
        }

        public void OnRoundEnd()
        {
            Singleton<SessionInfoPacketHandler>.Instance.Send();
        }
    }

    public class GameModeTicker : MonoBehaviour
    {
        // UI Styling
        private GUIStyle _headerStyle;      // For "PLAYER", "K", "D" headers
        private GUIStyle _rowStyle;         // For actual player names/stats
        private GUIStyle _timerStyle;       // For the main clock
        private GUIStyle _scoreBigStyle;    // For Team Scores

        // Textures
        private Texture2D _darkBackground;
        private Texture2D _rowHighlight;
        private bool _stylesInitialized = false;

        // Layout Constants
        private const float TOP_BAR_WIDTH = 400f;
        private const float TOP_BAR_HEIGHT = 60f;
        private const float SB_WIDTH = 800f;
        private const float SB_HEIGHT = 500f;


        private void OnGUI()
        {
            if (!Singleton<BaseGameMode>.Instantiated) return;
            var game = Singleton<BaseGameMode>.Instance;
            if (game.session == null) return;

            if (!_stylesInitialized) InitStyles();

            // 1. Always Draw HUD (Timer & Scores)
            DrawTopBar(game);

            // 2. Only Draw Scoreboard on TAB
            if (Input.GetKey(KeyCode.Tab))
            {
                DrawScoreboard(game);
            }
        }

        // ---------------------------------------------------------
        // HUD DRAWING
        // ---------------------------------------------------------
        private void DrawTopBar(BaseGameMode game)
        {
            float screenCX = Screen.width / 2f;

            // Define the main area for the top bar
            Rect topBarRect = new Rect(screenCX - (TOP_BAR_WIDTH / 2), 0, TOP_BAR_WIDTH, TOP_BAR_HEIGHT);

            // Draw Background Box
            GUI.DrawTexture(topBarRect, _darkBackground);

            // -- BEAR SCORE (Left) --
            // Aligned to the left side of the bar
            Rect bearRect = new Rect(topBarRect.x, topBarRect.y, 100, topBarRect.height - 20);
            int bearScore = game.session.factionWins.ContainsKey(Faction.T) ? game.session.factionWins[Faction.T] : 0;

            GUI.Label(bearRect, "T", _headerStyle); // Label above
            Rect bearScoreRect = new Rect(bearRect.x, bearRect.y + 15, bearRect.width, bearRect.height);
            GUI.Label(bearScoreRect, bearScore.ToString(), _scoreBigStyle);

            // -- USEC SCORE (Right) --
            // Aligned to the right side of the bar
            Rect usecRect = new Rect(topBarRect.x + TOP_BAR_WIDTH - 100, topBarRect.y, 100, topBarRect.height - 20);
            int usecScore = game.session.factionWins.ContainsKey(Faction.CT) ? game.session.factionWins[Faction.CT] : 0;

            GUI.Label(usecRect, "CT", _headerStyle);
            Rect usecScoreRect = new Rect(usecRect.x, usecRect.y + 15, usecRect.width, usecRect.height);
            GUI.Label(usecScoreRect, usecScore.ToString(), _scoreBigStyle);

            // -- TIMER (Center) --
            Rect timerRect = new Rect(screenCX - 50, 5, 100, TOP_BAR_HEIGHT);
            string timeStr = FormatTime(game.StateTimer);
            GUI.Label(timerRect, timeStr, _timerStyle);

            // Optional: Small state text under timer
            Rect stateRect = new Rect(screenCX - 50, 40, 100, 20);
            GUI.Label(stateRect, game.session.roundState.ToString().ToUpper(), _headerStyle);
        }

        // ---------------------------------------------------------
        // SCOREBOARD DRAWING
        // ---------------------------------------------------------
        private void DrawScoreboard(BaseGameMode game)
        {
            float boxX = (Screen.width - SB_WIDTH) / 2f;
            float boxY = (Screen.height - SB_HEIGHT) / 2f;

            // Main Background
            GUI.DrawTexture(new Rect(boxX, boxY, SB_WIDTH, SB_HEIGHT), _darkBackground);

            // Define Column Offsets (Relative to BoxX)
            float colName = 20f;
            float colFac = 300f;
            float colK = 450f;
            float colD = 525f;
            float colA = 600f;
            float colStatus = 675f;

            float rowHeight = 35f;
            float currentY = boxY + 20f;

            // -- HEADERS --
            GUI.Label(new Rect(boxX + colName, currentY, 200, rowHeight), "PLAYER", _headerStyle);
            GUI.Label(new Rect(boxX + colFac, currentY, 100, rowHeight), "FACTION", _headerStyle);
            GUI.Label(new Rect(boxX + colK, currentY, 50, rowHeight), "K", _headerStyle);
            GUI.Label(new Rect(boxX + colD, currentY, 50, rowHeight), "D", _headerStyle);
            GUI.Label(new Rect(boxX + colA, currentY, 50, rowHeight), "A", _headerStyle);
            GUI.Label(new Rect(boxX + colStatus, currentY, 100, rowHeight), "STATUS", _headerStyle);

            currentY += 40f; // Spacing after header

            // Sort logic: Alive first, then Kills
            var sortedPlayers = game.session.scoreboard.Values
                .OrderByDescending(p => p.kills)
                .ToList();

            foreach (var p in sortedPlayers)
            {
                // Safety check for null player object
                string pName = p.player?.Profile?.Nickname ?? "Connecting...";
                string factionStr = p.faction.ToString();

                // Draw Row Background (Highlight if dead)
                Rect rowRect = new Rect(boxX, currentY, SB_WIDTH, rowHeight);
                if (!p.isAlive)
                {
                    GUI.color = new Color(1f, 0.5f, 0.5f, 0.3f); // Reddish tint
                    GUI.DrawTexture(rowRect, _rowHighlight);
                    GUI.color = Color.white;
                }
                else if (p.player != null && p.player.Id == Singleton<GameWorld>.Instance.MainPlayer.Id)
                {
                    // Optional: Highlight "Me"
                    GUI.color = new Color(1f, 1f, 1f, 0.1f);
                    GUI.DrawTexture(rowRect, _rowHighlight);
                    GUI.color = Color.white;
                }

                // Draw Text
                // Gray out text if dead
                if (!p.isAlive) GUI.color = Color.gray;

                GUI.Label(new Rect(boxX + colName, currentY, 250, rowHeight), pName, _rowStyle);
                GUI.Label(new Rect(boxX + colFac, currentY, 100, rowHeight), factionStr, _rowStyle);
                GUI.Label(new Rect(boxX + colK, currentY, 50, rowHeight), p.kills.ToString(), _rowStyle);
                GUI.Label(new Rect(boxX + colD, currentY, 50, rowHeight), p.deaths.ToString(), _rowStyle);
                GUI.Label(new Rect(boxX + colA, currentY, 50, rowHeight), p.assists.ToString(), _rowStyle);

                // Status Logic
                string statusText = p.isAlive ? "ALIVE" : "DEAD";

                // If in warmup, show Ready status
                if (game.session.roundState == RoundState.Warmup)
                {
                    statusText = p.isReady ? "READY" : "WAITING";
                    GUI.color = p.isReady ? Color.green : Color.yellow;
                }
                else
                {
                    GUI.color = p.isAlive ? Color.green : Color.red;
                }

                GUI.Label(new Rect(boxX + colStatus, currentY, 100, rowHeight), statusText, _rowStyle);

                // Reset Color and move down
                GUI.color = Color.white;
                currentY += rowHeight;
            }
        }

        // ---------------------------------------------------------
        // HELPERS
        // ---------------------------------------------------------
        private string FormatTime(float seconds)
        {
            if (seconds < 0) seconds = 0;
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
        }

        private void InitStyles()
        {
            // Backgrounds
            _darkBackground = MakeTex(2, 2, new Color(0, 0, 0, 0.85f));
            _rowHighlight = MakeTex(2, 2, new Color(1, 1, 1, 1f)); // White base, tinted by GUI.color

            // Fonts
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

            _rowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft // Left align looks better for lists
            };
            _rowStyle.normal.textColor = Color.white;

            _timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _timerStyle.normal.textColor = new Color(1f, 0.8f, 0.2f); // Gold

            _scoreBigStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _scoreBigStyle.normal.textColor = Color.white;

            _stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void Update()
        {
            if (Singleton<BaseGameMode>.Instantiated)
            {
                Singleton<BaseGameMode>.Instance.Update();
            }
        }


    }

    public class StateWarmup : IGameState
    {
        public RoundState StateType => RoundState.Warmup;

        public void OnEnter(BaseGameMode game)
        {
            // Server sets authoritative duration; client derives remaining from replication.
            if (FikaBackendUtils.IsServer)
            {
                game.StateTimer = 45f;
            }
        }

        public void OnUpdate(BaseGameMode game)
        {
            if (!FikaBackendUtils.IsServer) return;
            bool allReady = game.session.scoreboard.Count > 0 && game.session.scoreboard.Values.All(p => p.isReady);

            if (allReady || game.StateTimer <= 0)
            {
                game.ChangeState(new StateWarmupEnd());
            }
        }

        public void OnExit(BaseGameMode game) { }
    }

    public class StateWarmupEnd : IGameState
    {
        public RoundState StateType => RoundState.WarmupEnd;

        public void OnEnter(BaseGameMode game)
        {
            if (FikaBackendUtils.IsServer)
            {
                game.StateTimer = 5f; // From your previous logic
            }
        }

        public void OnUpdate(BaseGameMode game)
        {
            if (!FikaBackendUtils.IsServer) return;
            if (game.StateTimer <= 0)
            {
                game.ChangeState(new StatePrepare());
            }
        }

        public void OnExit(BaseGameMode game) { }
    }

    public class StatePrepare : IGameState
    {
        public RoundState StateType => RoundState.Prepare;

        public void OnEnter(BaseGameMode game)
        {
            // Shared behavior: round reset flags.
            foreach (var p in game.session.scoreboard)
                p.Value.isAlive = true;

            // Client-only side effects (dedicated server safe)
            var mainPlayer = Singleton<GameWorld>.Instance?.MainPlayer;
            if (mainPlayer != null)
            {
                Teleporter.Teleport(mainPlayer);
                Patch_Kill.FixMe(mainPlayer.ActiveHealthController);
            }

            if (FikaBackendUtils.IsServer)
            {
                game.StateTimer = 5f; // Freeze time
            }
        }

        public void OnUpdate(BaseGameMode game)
        {
            if (!FikaBackendUtils.IsServer) return;
            if (game.StateTimer <= 0)
            {
                game.ChangeState(new StateAction());
            }
        }

        public void OnExit(BaseGameMode game) { }
    }

    public class StateAction : IGameState
    {
        public RoundState StateType => RoundState.Action;

        public void OnEnter(BaseGameMode game)
        {
            if (FikaBackendUtils.IsServer)
            {
                game.StateTimer = 30f; // Live round timer (10s for testing, change to 120s usually)
            }
        }

        public void OnUpdate(BaseGameMode game)
        {
            if (!FikaBackendUtils.IsServer) return;
            Faction? winningFaction = CheckFactionElimination(game);

            if (winningFaction.HasValue)
            {
                AwardRound(game, winningFaction.Value);
                game.ChangeState(new StateEnd());
                return;
            }

            if (game.session.bombState == BombState.Planted)
            {
                game.ChangeState(new StatePlanted());
            }

            if (game.StateTimer <= 0)
            {
                AwardRound(game, Faction.CT);
                game.ChangeState(new StateEnd());
            }
        }

        private Faction? CheckFactionElimination(BaseGameMode game)
        {
            var aliveByFaction = game.session.scoreboard.Values
                .Where(p => p.isAlive)
                .GroupBy(p => p.faction)
                .ToDictionary(g => g.Key, g => g.Count());

            // Get all factions currently in match
            var allFactions = game.session.scoreboard.Values
                .Select(p => p.faction)
                .Distinct()
                .Where(f => f != Faction.None)
                .ToList();

            foreach (var faction in allFactions)
            {
                if (!aliveByFaction.ContainsKey(faction) || aliveByFaction[faction] == 0)
                {
                    // This faction is wiped → other faction wins
                    return allFactions.FirstOrDefault(f => f != faction);
                }
            }

            return null;
        }

        private void AwardRound(BaseGameMode game, Faction winner)
        {
            if (!game.session.factionWins.ContainsKey(winner))
                game.session.factionWins[winner] = 0;

            game.session.factionWins[winner]++;
        }

        public void OnExit(BaseGameMode game) { }
    }

    public class StatePlanted : IGameState
    {
        public RoundState StateType => RoundState.Planted;

        public void OnEnter(BaseGameMode game)
        {
            if (FikaBackendUtils.IsServer)
            {
                game.StateTimer = 45f;
            }
        }

        public void OnUpdate(BaseGameMode game)
        {
            if (!FikaBackendUtils.IsServer) return;
            Faction? winningFaction = CheckFactionElimination(game);

            if (winningFaction.HasValue)
            {
                AwardRound(game, winningFaction.Value);
                game.ChangeState(new StateEnd());
                return;
            }

            if (game.StateTimer <= 0)
            {
                AwardRound(game, Faction.T);
                game.ChangeState(new StateEnd());
            }
        }

        private Faction? CheckFactionElimination(BaseGameMode game)
        {
            var aliveByFaction = game.session.scoreboard.Values
                .Where(p => p.isAlive)
                .GroupBy(p => p.faction)
                .ToDictionary(g => g.Key, g => g.Count());

            // Get all factions currently in match
            var allFactions = game.session.scoreboard.Values
                .Select(p => p.faction)
                .Distinct()
                .Where(f => f != Faction.None)
                .ToList();

            if (!aliveByFaction.ContainsKey(Faction.CT) || aliveByFaction[Faction.CT] == 0)
            {
                return Faction.T;
            }

            return null;
        }

        private void AwardRound(BaseGameMode game, Faction winner)
        {
            if (!game.session.factionWins.ContainsKey(winner))
                game.session.factionWins[winner] = 0;

            game.session.factionWins[winner]++;
        }

        public void OnExit(BaseGameMode game) { }
    }

    public class StateEnd : IGameState
    {
        public RoundState StateType => RoundState.End;

        public void OnEnter(BaseGameMode game)
        {
            if (FikaBackendUtils.IsServer)
            {
                game.StateTimer = 10f; // Scoreboard showing time
            }
            if (FikaBackendUtils.IsServer)
            {
                game.OnRoundEnd();     // Sync the scoreboard data to clients
            }
        }

        public void OnUpdate(BaseGameMode game)
        {
            if (!FikaBackendUtils.IsServer) return;
            if (game.StateTimer <= 0)
            {
                // Go to next round
                game.ChangeState(new StatePrepare());
            }
        }

        public void OnExit(BaseGameMode game) { }
    }
}