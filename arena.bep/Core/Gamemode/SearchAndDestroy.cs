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
        public void OnEnter() { if (FikaBackendUtils.IsServer) H.Arena.StateTimer = 120f; }
        public MatchState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
            Faction? winner = CheckWipe();
            if (winner.HasValue) { Award(winner.Value); return MatchState.RoundEnd; }
            if (H.Session.bombState == BombState.Planted) return MatchState.RoundPlanted;
            if (H.Arena.StateTimer <= 0) { Award(Faction.CT); return MatchState.RoundEnd; }
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

        private void Award(Faction w)
        {
            if (!H.Session.factionWins.ContainsKey(w))
                H.Session.factionWins[w] = 0;
            H.Session.factionWins[w]++;
        }
    }

    public class SnDPlanted : IGameState
    {
        public MatchState StateType => MatchState.RoundPlanted;
        public void OnEnter() { if (FikaBackendUtils.IsServer) H.Arena.StateTimer = 45f; }
        public MatchState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (!H.Scoreboard.Values.Any(p => p.isAlive && p.faction == Faction.CT)) { Award(Faction.T); return MatchState.RoundEnd; }
            if (H.Arena.StateTimer <= 0) { Award(Faction.T); return MatchState.RoundEnd; }
            return null;
        }
        public void OnExit() { }

        private void Award(Faction w)
        {
            if (!H.Session.factionWins.ContainsKey(w))
                H.Session.factionWins[w] = 0;
            H.Session.factionWins[w]++;
        }
    }

    public class SnDModeRules : GameModeRules
    {
        public int maxRoundsToWin = 13;

        public float platingTime = 4.5f;
        public float defusingTime = 5f;

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
