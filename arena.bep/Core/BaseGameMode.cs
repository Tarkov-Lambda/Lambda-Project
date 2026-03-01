using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Networking;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
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
            if (session != null) return;

            session = new SessionInfo();

            // Singleton<RestartPacketHandler>.Instance.Send();
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

            Singleton<RoundStatePacketHandler>.Instance.Send(_currentState.StateType);

            _currentState.OnEnter(this);
        }

        public void OnRoundEnd()
        {
            Singleton<SessionInfoPacketHandler>.Instance.Send();
        }
    }


    public class GameModeTicker : MonoBehaviour
    {
        private void Update()
        {
            if (Singleton<BaseGameMode>.Instantiated)
            {
                Singleton<BaseGameMode>.Instance.Update();
            }
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
            game.StateTimer = 45f;
        }

        public void OnUpdate(BaseGameMode game)
        {
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
            foreach (var p in game.session.scoreboard)
            {
                p.Value.isAlive = true;
            }
            game.StateTimer = 5f; // Freeze time
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
            game.StateTimer = 30f; // Live round timer (10s for testing, change to 120s usually)
        }

        public void OnUpdate(BaseGameMode game)
        {
            Faction? winningFaction = CheckFactionElimination(game);

            if (winningFaction.HasValue)
            {
                AwardRound(game, winningFaction.Value);
                game.ChangeState(new StateEnd());
                return;
            }

            if (game.StateTimer <= 0)
            {
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

            // Optional: log
            // Plugin.Logger.LogInfo($"Faction {winner} wins the round!");
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
}