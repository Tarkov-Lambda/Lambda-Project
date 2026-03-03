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
        public void OnEnter() { if (FikaBackendUtils.IsServer) H.game.StateTimer = 120f; }
        public RoundState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
            Faction? winner = CheckWipe();
            if (winner.HasValue) { Award(winner.Value); return RoundState.End; }
            if (H.session.bombState == BombState.Planted) return RoundState.Planted;
            if (H.game.StateTimer <= 0) { Award(Faction.CT); return RoundState.End; }
            return null;
        }
        public void OnExit() { }

        private Faction? CheckWipe()
        {
            var alive = H.scoreboard.Values.Where(p => p.isAlive).GroupBy(p => p.faction).ToDictionary(g => g.Key, g => g.Count());
            var factions = H.scoreboard.Values.Select(p => p.faction).Where(f => f != Faction.None).Distinct();
            foreach (var f in factions) if (!alive.ContainsKey(f) || alive[f] == 0) return factions.FirstOrDefault(o => o != f);
            return null;
        }

        private void Award(Faction w)
        {
            if (!H.session.factionWins.ContainsKey(w))
                H.session.factionWins[w] = 0;
            H.session.factionWins[w]++;
        }
    }

    public class SnDPlanted : IGameState
    {
        public RoundState StateType => RoundState.Planted;
        public void OnEnter() { if (FikaBackendUtils.IsServer) H.game.StateTimer = 45f; }
        public RoundState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (!H.scoreboard.Values.Any(p => p.isAlive && p.faction == Faction.CT)) { Award(Faction.T); return RoundState.End; }
            if (H.game.StateTimer <= 0) { Award(Faction.T); return RoundState.End; }
            return null;
        }
        public void OnExit() { }

        private void Award(Faction w)
        {
            if (!H.session.factionWins.ContainsKey(w))
                H.session.factionWins[w] = 0;
            H.session.factionWins[w]++;
        }
    }

    public class SnDModeRules : GameModeRules
    {
        public int maxRoundsToWin = 13;

        public float prepareTime = 120;
        public float roundTime = 120;
        public float plantTime = 120;

        public float platingTime = 4.5f;
        public float defusingTime = 5f;

        public override IGameState CreateState(RoundState state) => state switch
        {
            RoundState.None => new SharedNone(),

            RoundState.Warmup => new SharedWarmup(),
            RoundState.WarmupEnd => new SharedWarmupEnd(),

            RoundState.Prepare => new SharedPrepare(),
            RoundState.Action => new SnDAction(),
            RoundState.Planted => new SnDPlanted(),
            RoundState.End => new SharedEnd(),

            RoundState.SideSwap => new SharedSideSwap(),
            RoundState.Finish => new SharedFinish(),
            _ => null
        };

        public override void DrawTopBar(Base game, Rect bounds, GUIStyle header, GUIStyle scoreBig, GUIStyle timer)
        {
            GUI.Label(new Rect(bounds.x, bounds.y, 100, bounds.height - 20), "T", header);
            GUI.Label(new Rect(bounds.x, bounds.y + 15, 100, bounds.height), H.game.session.factionWins.GetValueOrDefault(Faction.T, 0).ToString(), scoreBig);
            GUI.Label(new Rect(bounds.x + bounds.width - 100, bounds.y, 100, bounds.height - 20), "CT", header);
            GUI.Label(new Rect(bounds.x + bounds.width - 100, bounds.y + 15, 100, bounds.height), H.game.session.factionWins.GetValueOrDefault(Faction.CT, 0).ToString(), scoreBig);
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 50, 5, 100, bounds.height), FormatTime(H.game.StateTimer), timer);
            GUI.Label(new Rect(bounds.x + bounds.width / 2f - 50, 40, 100, 20), H.game.session.roundState.ToString().ToUpper(), header);
        }
    }

}
