using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using ifp.arena.bep.Dying;
using ifp.arena.bep.Networking;
using ifp.arena.bep.Patches;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.bep.GameTypes
{
    // ---------------------------------------------------------
    // 1. STATE MACHINE INTERFACE
    // ---------------------------------------------------------
    public interface IGameState
    {
        RoundState StateType { get; }
        void OnEnter(BaseGameMode gameMode);
        void OnUpdate(BaseGameMode gameMode);
        void OnExit(BaseGameMode gameMode);
    }

    // ---------------------------------------------------------
    // 2. MAIN GAME MODE CONTROLLER (THE CONTEXT)
    // ---------------------------------------------------------
    public class BaseGameMode : Singleton<BaseGameMode>, IDisposable
    {
        public SessionInfo session;
        public float StateTimer;

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

            // Create a hidden GameObject to run Unity's Update loop for our State Machine
            _tickerObject = new GameObject("SnD_GameModeTicker");
            _tickerObject.AddComponent<GameModeTicker>();
            UnityEngine.Object.DontDestroyOnLoad(_tickerObject);
        }

        public void Dispose()
        {
            Patch_Gameworld_OnGameStarted.OnGameStarted -= StartSession;

            if (_tickerObject != null)
            {
                UnityEngine.Object.Destroy(_tickerObject);
            }

            Release(this);
        }

        public void StartSession(GameWorld gameWorld)
        {
            session = new SessionInfo();

            // The Server is the absolute authority over the state machine.
            if (FikaBackendUtils.IsServer)
            {
                ChangeState(new StateWarmup());
            }
        }

        // Called by the GameModeTicker every Unity frame
        public void Update()
        {
            if (!FikaBackendUtils.IsServer) return;

            if (_currentState != null)
            {
                // Subtract delta time from our state timer
                StateTimer -= Time.deltaTime;
                _currentState.OnUpdate(this);
            }
        }

        public void ChangeState(IGameState newState)
        {
            if (_currentState != null)
            {
                _currentState.OnExit(this);
            }

            _currentState = newState;
            session.roundState = _currentState.StateType;

            // Immediately broadcast the new state to all clients
            Singleton<RoundStatePacketHandler>.Instance.Send(_currentState.StateType);

            _currentState.OnEnter(this);

            // Assumes you have a Plugin class with a Logger
            // Plugin.Logger.LogInfo($"[Server] Transitioned to {_currentState.StateType}");
        }

        public void OnRoundEnd()
        {
            Singleton<SessionInfoPacketHandler>.Instance.Send();
        }
    }

    // ---------------------------------------------------------
    // 3. UNITY UPDATE TICKER
    // ---------------------------------------------------------
    // Because Comfort.Common.Singleton is a standard C# class and not a MonoBehaviour, 
    // it doesn't have an Update() method. This component bridges that gap.
    public class GameModeTicker : MonoBehaviour
    {
        private void Update()
        {
            if (Singleton<BaseGameMode>.Instantiated)
            {
                Singleton<BaseGameMode>.Instance.Update();
            }
        }

        private void FixedUpdate()
        {
            //Plugin.Logger
        }
    }

    // ---------------------------------------------------------
    // 4. CONCRETE STATES (THE LOGIC)
    // ---------------------------------------------------------

    public class StateWarmup : IGameState
    {
        public RoundState StateType => RoundState.Warmup;

        public void OnEnter(BaseGameMode game)
        {
            // Example: 15 seconds of warmup
            game.StateTimer = 15f;
        }

        public void OnUpdate(BaseGameMode game)
        {
            if (game.StateTimer <= 0)
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
            game.StateTimer = 5f; // From your previous logic
        }

        public void OnUpdate(BaseGameMode game)
        {
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
            game.StateTimer = 5f; // Freeze time

            // NOTE: Teleport the HOST local player on the server
            // Clients must teleport themselves in RoundStatePacketHandler.OnReceive()
            if (Singleton<GameWorld>.Instance?.MainPlayer != null)
            {
                Teleporter.Teleport(Singleton<GameWorld>.Instance.MainPlayer);
            }
        }

        public void OnUpdate(BaseGameMode game)
        {
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
            game.StateTimer = 10f; // Live round timer (10s for testing, change to 120s usually)
        }

        public void OnUpdate(BaseGameMode game)
        {
            // Check for Win Conditions here (e.g. Everyone dead, Bomb Planted)

            // Check for Time running out
            if (game.StateTimer <= 0)
            {
                game.ChangeState(new StateEnd());
            }
        }

        public void OnExit(BaseGameMode game) { }
    }

    public class StateEnd : IGameState
    {
        public RoundState StateType => RoundState.End;

        public void OnEnter(BaseGameMode game)
        {
            game.StateTimer = 10f; // Scoreboard showing time
            game.OnRoundEnd();     // Sync the scoreboard data to clients
        }

        public void OnUpdate(BaseGameMode game)
        {
            if (game.StateTimer <= 0)
            {
                // Go to next round
                game.ChangeState(new StatePrepare());
            }
        }

        public void OnExit(BaseGameMode game) { }
    }

    // ---------------------------------------------------------
    // 5. ENUMS AND DATA CLASSES
    // ---------------------------------------------------------

    public enum RoundState
    {
        None,
        Warmup,
        WarmupEnd,
        Prepare,
        Action,
        End
    }

    public class SessionInfo
    {
        public RoundState roundState = RoundState.None;
        public Dictionary<int, PlayerScore> scoreboard = new Dictionary<int, PlayerScore>();
        public Dictionary<Faction, int> factionWins = new Dictionary<Faction, int>();

        public GameModes currentGameMode = GameModes.SND;
        public string mapName = "gold_dust2";

        public SessionInfo()
        {
            InitializeScoreBoard();
        }

        public void InitializeScoreBoard()
        {
            if (Singleton<GameWorld>.Instance == null || Singleton<GameWorld>.Instance.AllAlivePlayersList == null)
                return;

            foreach (var p in Singleton<GameWorld>.Instance.AllAlivePlayersList.ToArray())
            {
                if (!scoreboard.ContainsKey(p.Id))
                {
                    scoreboard[p.Id] = new PlayerScore();
                }
            }
        }

        // Locking out player shooting, moving, jumping during certain session states
        public bool IsControllerPartiallyLocked()
        {
            if (roundState == RoundState.None || roundState == RoundState.Prepare) return true;
            return false;
        }
    }

    public class PlayerScore
    {
        public Faction faction = Faction.None;
        public int kills = 0;
        public int assists = 0;
        public int deaths = 0;
    }

    public enum BombState
    {
        None,
        Planting,
        Planted,
        Defusing,
        Defused,
        Exploded
    }
}