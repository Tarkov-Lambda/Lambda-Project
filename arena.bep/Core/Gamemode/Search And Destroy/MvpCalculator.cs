using ifp.arena.bep.Core;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ifp.arena.bep.Core.Gamemode
{
    public static class MvpCalculator
    {
        public static int CalculateRoundMvp(Faction winner, RoundWinReason winReason, BombState objectiveBombState, int objectivePlayerId, System.Random rng = null)
        {
            if (winner is Faction.None) return -1;

            rng ??= new Random();

            var winners = H.Scoreboard
                .Where(kvp => kvp.Value != null && kvp.Value.faction == winner)
                .Select(kvp => kvp.Value)
                .ToList();

            if (winners.Count == 0) return -1;

            bool HasAnyStats(PlayerScore p) => p.kills != 0 || p.assists != 0 || p.deaths != 0;

            // No-action edge case: time ran out, no objective, and the winning team recorded zero stats.
            if (winReason == RoundWinReason.Timeout
                && objectiveBombState == BombState.None
                && winners.All(p => !HasAnyStats(p)))
            {
                return -1;
            }

            // Objective priority: defuse/explosion should pick objective player.
            if (winReason == RoundWinReason.Objective
                && objectivePlayerId > 0)
            {
                // Defuse exception: defuser w/ exactly 0 kills can lose MVP to top-frag teammate.
                if (objectiveBombState == BombState.Defused)
                {
                    var defuser = winners.FirstOrDefault(p => p.player != null && p.player.Id == objectivePlayerId);
                    if (defuser != null && defuser.kills == 0)
                    {
                        int maxKills = winners.Max(p => p.kills);
                        if (maxKills > 0)
                            return BreakTies(winners.Where(p => p.kills == maxKills).ToList(), rng);
                    }
                }

                return objectivePlayerId;
            }

            // Elimination/timeout fallback: most kills.
            int bestKills = winners.Max(p => p.kills);
            return BreakTies(winners.Where(p => p.kills == bestKills).ToList(), rng);
        }

        private static int BreakTies(List<PlayerScore> tiedOnKills, System.Random rng)
        {
            if (tiedOnKills == null || tiedOnKills.Count == 0) return -1;
            if (tiedOnKills.Count == 1) return tiedOnKills[0].player?.Id ?? -1;

            int bestAssists = tiedOnKills.Max(p => p.assists);
            var tied = tiedOnKills.Where(p => p.assists == bestAssists).ToList();
            if (tied.Count == 1) return tied[0].player?.Id ?? -1;

            int idx = rng.Next(tied.Count);
            return tied[idx].player?.Id ?? -1;
        }
    }
}
