using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Patches.Tarkov;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ifp.arena.bep.Core.Gamemode
{
    // just kind of a "nothing is happening" state type beat
    public class SharedNone : IGameState
    {
        public RoundState StateType => RoundState.None;
        public void OnEnter(Base game) { Teleporter.Teleport(Singleton<GameWorld>.Instance.MainPlayer); }
        public RoundState? OnUpdate(Base game)
        {
            return null;
        }
        public void OnExit(Base game) { }
    }

    public class SharedWarmup : IGameState
    {
        public RoundState StateType => RoundState.Warmup;
        public void OnEnter(Base game) { 
            if (FikaBackendUtils.IsServer) game.StateTimer = 45f;
        }

        public RoundState? OnUpdate(Base game)
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (game.StateTimer <= 0 || game.session.scoreboard.Count > 0 && game.session.scoreboard.Values.All(p => p.isReady))
                return RoundState.WarmupEnd;
            return null;
        }
        public void OnExit(Base game) {
        }
    }

    public class SharedWarmupEnd : IGameState
    {
        public RoundState StateType => RoundState.WarmupEnd;
        public void OnEnter(Base game) { if (FikaBackendUtils.IsServer) game.StateTimer = 5f; }
        public RoundState? OnUpdate(Base game) => FikaBackendUtils.IsServer && game.StateTimer <= 0 ? RoundState.Prepare : null;
        public void OnExit(Base game) { }
    }

    public class SharedPrepare : IGameState
    {
        public RoundState StateType => RoundState.Prepare;
        public void OnEnter(Base game)
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
        public RoundState? OnUpdate(Base game) => FikaBackendUtils.IsServer && game.StateTimer <= 0 ? RoundState.Action : null;
        public void OnExit(Base game) { }
    }

    public class SharedEnd : IGameState
    {
        public RoundState StateType => RoundState.End;
        public void OnEnter(Base game) { if (FikaBackendUtils.IsServer) { game.StateTimer = 10f; game.OnRoundEnd(); } }
        public RoundState? OnUpdate(Base game) => FikaBackendUtils.IsServer && game.StateTimer <= 0 ? RoundState.Prepare : null;
        public void OnExit(Base game) { }
    }
}
