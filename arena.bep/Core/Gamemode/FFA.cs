using Fika.Core.Main.Utils;
using ifp.arena.bep.GameTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ifp.arena.bep.Core.Gamemode;

public class FFAAction : IGameState
{
    public MatchState StateType => MatchState.RoundAction;
    public void OnEnter() { }
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

}

