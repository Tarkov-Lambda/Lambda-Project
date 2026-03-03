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
        public RoundState StateType => RoundState.Action;
        public void OnEnter(Base game) { if (FikaBackendUtils.IsServer) game.StateTimer = 120f; }
        public RoundState? OnUpdate(Base game)
        {
            if (!FikaBackendUtils.IsServer) return null;
            Faction? winner = CheckWipe(game);
            if (winner.HasValue) { Award(game, winner.Value); return RoundState.End; }
            if (game.session.bombState == BombState.Planted) return RoundState.Planted;
            if (game.StateTimer <= 0) { Award(game, Faction.CT); return RoundState.End; }
            return null;
        }
        public void OnExit(Base game) { }

        private Faction? CheckWipe(Base game)
        {
            var alive = game.session.scoreboard.Values.Where(p => p.isAlive).GroupBy(p => p.faction).ToDictionary(g => g.Key, g => g.Count());
            var factions = game.session.scoreboard.Values.Select(p => p.faction).Where(f => f != Faction.None).Distinct();
            foreach (var f in factions) if (!alive.ContainsKey(f) || alive[f] == 0) return factions.FirstOrDefault(o => o != f);
            return null;
        }
        private void Award(Base game, Faction w) { if (!game.session.factionWins.ContainsKey(w)) game.session.factionWins[w] = 0; game.session.factionWins[w]++; }
    }

    public class SnDPlanted : IGameState
    {
        public RoundState StateType => RoundState.Planted;
        public void OnEnter(Base game) { if (FikaBackendUtils.IsServer) game.StateTimer = 45f; }
        public RoundState? OnUpdate(Base game)
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (!game.session.scoreboard.Values.Any(p => p.isAlive && p.faction == Faction.CT)) { Award(game, Faction.T); return RoundState.End; }
            if (game.StateTimer <= 0) { Award(game, Faction.T); return RoundState.End; }
            return null;
        }
        public void OnExit(Base game) { }
        private void Award(Base game, Faction w) { if (!game.session.factionWins.ContainsKey(w)) game.session.factionWins[w] = 0; game.session.factionWins[w]++; }
    }

    public class SnDModeRules : GameModeRules
    {
        public override IGameState CreateState(RoundState state) => state switch
        {
            RoundState.None => new SharedNone(),
            RoundState.Warmup => new SharedWarmup(),
            RoundState.WarmupEnd => new SharedWarmupEnd(),
            RoundState.Prepare => new SharedPrepare(),
            RoundState.Action => new SnDAction(),
            RoundState.Planted => new SnDPlanted(),
            RoundState.End => new SharedEnd(),
            _ => null
        };

        public override void DrawTopBar(Base game, Rect bounds, GUIStyle header, GUIStyle scoreBig, GUIStyle timer)
        {
            GUI.Label(new Rect(bounds.x, bounds.y, 100, bounds.height - 20), "T", header);
            GUI.Label(new Rect(bounds.x, bounds.y + 15, 100, bounds.height), game.session.factionWins.GetValueOrDefault(Faction.T, 0).ToString(), scoreBig);
            GUI.Label(new Rect(bounds.x + bounds.width - 100, bounds.y, 100, bounds.height - 20), "CT", header);
            GUI.Label(new Rect(bounds.x + bounds.width - 100, bounds.y + 15, 100, bounds.height), game.session.factionWins.GetValueOrDefault(Faction.CT, 0).ToString(), scoreBig);
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 50, 5, 100, bounds.height), FormatTime(game.StateTimer), timer);
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 50, 40, 100, 20), game.session.roundState.ToString().ToUpper(), header);
        }
    }

}
