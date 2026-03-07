using Fika.Core.Main.Utils;
using ifp.arena.bep.GameTypes;
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
            if (winner.HasValue) { Award(winner.Value, RoundWinReason.Elimination); return MatchState.RoundEnd; }
            if (H.Session.bombState == BombState.Planted) return MatchState.RoundPlanted;
            if (H.Arena.StateTimer <= 0) { Award(Faction.CT, RoundWinReason.Timeout); return MatchState.RoundEnd; }
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

        private void Award(Faction w, RoundWinReason reason)
        {
            if (!H.Session.factionWins.ContainsKey(w))
                H.Session.factionWins[w] = 0;
            H.Session.factionWins[w]++;

            int mvpId = MvpCalculator.CalculateRoundMvp(w, reason, H.Arena.LastObjectiveBombState, H.Arena.LastObjectivePlayerId);


            H.Arena.PendingRoundActionEnd = new RoundActionPhaseEnd { mvpId = mvpId, winner = w, roundWinReason = reason };
        }
    }

    public class SnDPlanted : IGameState
    {
        public MatchState StateType => MatchState.RoundPlanted;
        public void OnEnter() { }
        public MatchState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (!H.Scoreboard.Values.Any(p => p.isAlive && p.faction == Faction.CT)) { AwardExploded(Faction.T); return MatchState.RoundEnd; }
            if (H.Arena.StateTimer <= 0) { AwardExploded(Faction.T); return MatchState.RoundEnd; }
            return null;
        }
        public void OnExit() { }

        private void AwardExploded(Faction w)
        {
            if (!H.Session.factionWins.ContainsKey(w))
                H.Session.factionWins[w] = 0;
            H.Session.factionWins[w]++;

            int mvpId = MvpCalculator.CalculateRoundMvp(w, RoundWinReason.Objective, BombState.Exploded, H.Arena.LastObjectivePlayerId);
            if (mvpId > 0 && H.Scoreboard.TryGetValue(mvpId, out var ps) && ps != null)
                ps.mvps++;

            H.Arena.PendingRoundActionEnd = new RoundActionPhaseEnd { mvpId = mvpId, winner = w, roundWinReason = RoundWinReason.Objective };
        }
    }

    public class SnDModeRules : GameModeRules
    {
        public static int maxRoundsToWin = 3;

        public static float platingTime = 4.5f;
        public static float defusingTime = 5f;

        public static string bombTemplateId = "57347da92459774491567cf5";

        public override IGameState CreateState(MatchState state) => state switch
        {
            MatchState.None => new SharedNone(),

            MatchState.Warmup => new SharedWarmup(),
            MatchState.WarmupEnd => new SharedWarmupEnd(),

            MatchState.RoundPrepare => new SharedPrepare(),
            MatchState.RoundAction => new SnDAction(),
            MatchState.RoundPlanted => new SnDPlanted(),
            MatchState.RoundEnd => new SharedEnd(),

            MatchState.SideSwap => new SharedSideSwap(),
            MatchState.MatchEnd => new SharedFinish(),
            _ => null
        };

        public override void DrawTopBar(ArenaController game, Rect bounds, GUIStyle header, GUIStyle scoreBig, GUIStyle timer)
        {
            GUI.Label(new Rect(bounds.x, bounds.y, 100, bounds.height - 20), "T", header);
            GUI.Label(new Rect(bounds.x, bounds.y + 15, 100, bounds.height), H.Arena.session.factionWins.GetValueOrDefault(Faction.T, 0).ToString(), scoreBig);
            GUI.Label(new Rect(bounds.x + bounds.width - 100, bounds.y, 100, bounds.height - 20), "CT", header);
            GUI.Label(new Rect(bounds.x + bounds.width - 100, bounds.y + 15, 100, bounds.height), H.Arena.session.factionWins.GetValueOrDefault(Faction.CT, 0).ToString(), scoreBig);
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 50, 5, 100, bounds.height), FormatTime(H.Arena.StateTimer), timer);
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 75, 40, 150, 20), H.Arena.session.roundState.ToString().ToUpper(), header);
        }
    }

}
