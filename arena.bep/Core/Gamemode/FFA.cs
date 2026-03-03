using Fika.Core.Main.Utils;
using ifp.arena.bep.GameTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ifp.arena.bep.Core.Gamemode
{
    public class FFAAction : IGameState
    {
        public RoundState StateType => RoundState.Action;
        public void OnEnter(Base game) { if (FikaBackendUtils.IsServer) game.StateTimer = 600f; } // 10 min
        public RoundState? OnUpdate(Base game)
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (game.StateTimer <= 0 || game.session.scoreboard.Values.Any(p => p.kills >= 20)) return RoundState.End;
            return null;
        }
        public void OnExit(Base game) { }
    }

    public class FFAModeRules : GameModeRules
    {
        public override IGameState CreateState(RoundState state) => state switch
        {
            RoundState.None => new SharedNone(),
            RoundState.Warmup => new SharedWarmup(),
            RoundState.WarmupEnd => new SharedWarmupEnd(),
            RoundState.Prepare => new SharedPrepare(),
            RoundState.Action => new FFAAction(),
            RoundState.End => new SharedEnd(),
            _ => null
        };

        public override void DrawTopBar(Base game, Rect bounds, GUIStyle header, GUIStyle scoreBig, GUIStyle timer)
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
