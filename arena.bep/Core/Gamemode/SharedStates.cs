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
        public void OnEnter() { Teleporter.Teleport(Singleton<GameWorld>.Instance.MainPlayer); }
        public RoundState? OnUpdate()
        {
            return null;
        }
        public void OnExit() { }
    }

    public class SharedWarmup : IGameState
    {
        public RoundState StateType => RoundState.Warmup;
        public void OnEnter() { 
            if (FikaBackendUtils.IsServer) H.game.StateTimer = 45f;
        }

        public RoundState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (H.game.StateTimer <= 0 || H.game.session.scoreboard.Count > 0 && H.game.session.scoreboard.Values.All(p => p.isReady))
                return RoundState.WarmupEnd;
            return null;
        }
        public void OnExit() {
        }
    }

    public class SharedWarmupEnd : IGameState
    {
        public RoundState StateType => RoundState.WarmupEnd;
        public void OnEnter() { if (FikaBackendUtils.IsServer) H.game.StateTimer = 5f; }
        public RoundState? OnUpdate() => FikaBackendUtils.IsServer && H.game.StateTimer <= 0 ? RoundState.Prepare : null;
        public void OnExit() { }
    }

    public class SharedPrepare : IGameState
    {
        public RoundState StateType => RoundState.Prepare;
        public void OnEnter()
        {
            foreach (var p in H.game.session.scoreboard.Values) p.isAlive = true;
            if (Singleton<GameWorld>.Instance?.MainPlayer != null)
            {
                Teleporter.Teleport(Singleton<GameWorld>.Instance.MainPlayer);
                Patch_Kill.FixMe(Singleton<GameWorld>.Instance.MainPlayer.ActiveHealthController);
            }
            if (FikaBackendUtils.IsServer) H.game.StateTimer = 5f;
            H.game.session.scoreboard.Clear();
            H.game.session.InitializeScoreBoard();
        }
        public RoundState? OnUpdate() => FikaBackendUtils.IsServer && H.game.StateTimer <= 0 ? RoundState.Action : null;
        public void OnExit() { }
    }

    public class SharedEnd : IGameState
    {
        public RoundState StateType => RoundState.End;
        public void OnEnter() { if (FikaBackendUtils.IsServer) { H.game.StateTimer = 10f; H.game.OnRoundEnd(); } }
        public RoundState? OnUpdate() => FikaBackendUtils.IsServer && H.game.StateTimer <= 0 ? RoundState.Prepare : null;
        public void OnExit() { }
    }
}
