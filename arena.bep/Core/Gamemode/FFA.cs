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
        public MatchState StateType => MatchState.RoundAction;
        public void OnEnter() { if (FikaBackendUtils.IsServer) H.Arena.StateTimer = 600f; } // 10 min
        public MatchState? OnUpdate()
        {
            if (FikaBackendUtils.IsClient) return null;
            if (H.Arena.StateTimer <= 0 || H.Scoreboard.Values.Any(p => p.kills >= 20)) return MatchState.MatchEnd;
            return null;
        }
        public void OnExit() { }
    }

    public class FFAModeRules : GameModeRules
    {
        public override IGameState CreateState(MatchState state) => state switch
        {
            MatchState.None => new SharedNone(),
            MatchState.Warmup => new SharedWarmup(),
            MatchState.WarmupEnd => new SharedWarmupEnd(),
            MatchState.RoundPrepare => new SharedPrepare(),
            MatchState.RoundAction => new FFAAction(),
            MatchState.MatchEnd => new SharedFinish(),
            _ => null
        };
        
        public override void DrawTopBar(ArenaController game, Rect bounds, GUIStyle header, GUIStyle scoreBig, GUIStyle timer)
        {
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 50, 5, 100, bounds.height), FormatTime(H.Arena.StateTimer), timer);
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 50, 40, 100, 20), "FFA", header);

            var top = H.Scoreboard.Values.OrderByDescending(p => p.kills).Take(2).ToList();
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
