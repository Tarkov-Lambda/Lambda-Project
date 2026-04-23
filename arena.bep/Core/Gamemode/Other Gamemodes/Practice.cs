using Fika.Core.Main.Utils;
using ifp.arena.bep.GameTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ifp.arena.bep.Core.Gamemode;


public class PracticeModeRules : LambdaGamemode
{
    
    public override IGameState CreateState(MatchState state) => state switch
    {
        MatchState.None => new SharedNone(),
        MatchState.Warmup => new SharedNone(),
        MatchState.WarmupEnd => new SharedNone(),
        MatchState.RoundPrepare => new SharedNone(),
        MatchState.RoundAction => new SharedNone(),
        MatchState.RoundEnd => new SharedNone(),
        _ => null
    };

}
