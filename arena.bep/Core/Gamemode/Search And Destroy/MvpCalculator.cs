using ifp.arena.bep.Core;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ifp.arena.bep.Core.Gamemode;

public static class MvpCalculator
{
    public static (int mvpId, string mvpReason) CalculateRoundMvp(Faction winner, RoundWinReason winReason, BombState objectiveBombState, int objectivePlayerId, System.Random rng = null)
    {
        if (winner is Faction.None) return (-1, null);

        rng ??= new Random();

        var winners = H.Scoreboard
            .Where(kvp => kvp.Value != null && kvp.Value.faction == winner)
            .Select(kvp => kvp.Value)
            .ToList();

        if (winners.Count == 0) return (-1, null);

        bool HasAnyStats(PlayerScore p) => p.kills != 0 || p.assists != 0 || p.deaths != 0;

        // No-action edge case: time ran out, no objective, and the winning team recorded zero stats.
        if (winReason == RoundWinReason.Timeout
            && objectiveBombState == BombState.None
            && winners.All(p => !HasAnyStats(p)))
        {
            return (-1, null);
        }

        // Objective priority: defuse/explosion should pick objective player.
        if (winReason == RoundWinReason.Objective
            && objectivePlayerId > 0)
        {
            // Planter: bomb exploded — planter always wins MVP.
            if (objectiveBombState == BombState.Exploded)
            {
                return (objectivePlayerId, "planting the bomb");
            }

            // Defuse exception: defuser w/ exactly 0 round kills can lose MVP to top-frag teammate.
            if (objectiveBombState == BombState.Defused)
            {
                var defuser = winners.FirstOrDefault(p => p.player != null && p.player.Id == objectivePlayerId);
                if (defuser != null && defuser.roundKills == 0)
                {
                    int maxKills = winners.Max(p => p.roundKills);
                    if (maxKills > 0)
                        return BreakTies(winners.Where(p => p.roundKills == maxKills).ToList(), rng);
                }

                return (objectivePlayerId, "defusing the bomb");
            }

            return (objectivePlayerId, null);
        }

        // Elimination/timeout fallback: most kills this round.
        int bestKills = winners.Max(p => p.roundKills);
        return BreakTies(winners.Where(p => p.roundKills == bestKills).ToList(), rng);
    }

    private static (int mvpId, string mvpReason) BreakTies(List<PlayerScore> tiedOnKills, System.Random rng)
    {
        if (tiedOnKills == null || tiedOnKills.Count == 0) return (-1, null);
        if (tiedOnKills.Count == 1) return (tiedOnKills[0].player?.Id ?? -1, "most kills this round");

        // Tiebreak 1: most headshots this round.
        int bestHeadshots = tiedOnKills.Max(p => p.roundHeadshots);
        if (bestHeadshots > 0)
        {
            var tiedOnHeadshots = tiedOnKills.Where(p => p.roundHeadshots == bestHeadshots).ToList();
            if (tiedOnHeadshots.Count == 1) return (tiedOnHeadshots[0].player?.Id ?? -1, "most headshots this round");

            // Tiebreak 2: most assists.
            int bestAssists = tiedOnHeadshots.Max(p => p.assists);
            var tiedOnAssists = tiedOnHeadshots.Where(p => p.assists == bestAssists).ToList();
            if (tiedOnAssists.Count == 1) return (tiedOnAssists[0].player?.Id ?? -1, "most assists this round");

            int idx = rng.Next(tiedOnAssists.Count);
            return (tiedOnAssists[idx].player?.Id ?? -1, "most kills this round");
        }

        // Tiebreak 2: most assists.
        int bestAssistsTop = tiedOnKills.Max(p => p.assists);
        var tied = tiedOnKills.Where(p => p.assists == bestAssistsTop).ToList();
        if (tied.Count == 1) return (tied[0].player?.Id ?? -1, "most assists this round");

        int randomIdx = rng.Next(tied.Count);
        return (tied[randomIdx].player?.Id ?? -1, "most kills this round");
    }
}
