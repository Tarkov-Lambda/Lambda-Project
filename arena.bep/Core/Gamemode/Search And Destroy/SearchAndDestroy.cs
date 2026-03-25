using Comfort.Common;
using Fika.Core.Main.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ifp.arena.bep.Core.Gamemode
{
    public class SnDAction : IGameState
    {
        public MatchState StateType => MatchState.RoundAction;
        public void OnEnter() { }
        public MatchState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
            Faction? winner = CheckWipe();
            if (winner.HasValue) { H.Arena.Award(winner.Value, RoundWinReason.Elimination); return MatchState.RoundEnd; }
            if (H.Session.bombState == BombState.Planted) return MatchState.RoundPlanted;
            if (H.Arena.StateTimer <= 0) { H.Arena.Award(Faction.CT, RoundWinReason.Timeout); return MatchState.RoundEnd; }
            return null;
        }
        public void OnExit() { }

        private Faction? CheckWipe()
        {
            var alive = H.Scoreboard.Values.Where(p => p.isAlive).GroupBy(p => p.faction).ToDictionary(g => g.Key, g => g.Count());
            var factions = H.Scoreboard.Values.Select(p => p.faction).Where(f => f != Faction.None).Distinct();
            foreach (var f in factions) if (!alive.ContainsKey(f) || alive[f] == 0) return factions.FirstOrDefault(o => o != f);
            return null;
        }
    }

    public class SnDPlanted : IGameState
    {
        public MatchState StateType => MatchState.RoundPlanted;

        public void OnEnter() { }

        public MatchState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;

            // If all CT are dead before timer runs out
            // if (!H.Scoreboard.Values.Any(p => p.isAlive && p.faction == Faction.CT))
            // {
            //     H.Arena.Award(Faction.T, RoundWinReason.Elimination);
            //     return MatchState.RoundEnd;
            // }

            if (H.Session.bombState == BombState.Defused)
            {
                H.Arena.Award(Faction.CT, RoundWinReason.Objective);
                return MatchState.RoundEnd;
            }

            if (H.Arena.StateTimer <= 0)
            {
                H.Arena.Award(Faction.T, RoundWinReason.Objective);
                Singleton<BombStatePacketHandler>.Instance.Send(H.MainPlayer, BombState.Exploded, Vector3.zero);
                return MatchState.RoundEnd;
            }

            return null;
        }

        public void OnExit() { }
    }

    public class SnDModeRules : GameModeRules
    {
        public static int maxRoundsToWin = 13;

        public static float platingTime = 4.5f;
        public static float defusingTime = 5f;
        public static float defuseRadius = 2.5f;

        public static string bombTemplateId = "628bc7fb408e2b2e9c0801b1";

        public override IGameState CreateState(MatchState state) => state switch
        {
            MatchState.None => new SharedNone(),

            MatchState.Warmup => new SharedWarmup(),
            MatchState.WarmupEnd => new SharedWarmupEnd(),

            MatchState.Pause => new SharedPause(),
            MatchState.RoundPrepare => new SharedPrepare(),
            MatchState.RoundAction => new SnDAction(),
            MatchState.RoundPlanted => new SnDPlanted(),
            MatchState.RoundEnd => new SharedEnd(),

            MatchState.SideSwap => new SharedSideSwap(),
            MatchState.MatchEnd => new SharedFinish(),
            _ => null
        };
    }

}
