using EFT;
using Lambda.Core.Main;
using Lambda.Core.GameTypes;
using Lambda.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lambda.Core.Main.Gamemode;

public static class MvpCalculator
{
    public static (int mvpId, string mvpReason) CalculateRoundMvp(Faction winner, RoundWinReason winReason, BombState objectiveBombState, Player objectivePlayer, System.Random rng = null)
    {
        if (winner is Faction.None) return (-1, null);

        rng ??= new Random();

        var winners = H.Scoreboard
            .Where(kvp => kvp.Value != null && kvp.Value.Faction == winner)
            .Select(kvp => kvp.Value)
            .ToList();

        if (winners.Count == 0) return (-1, null);

        bool HasAnyStats(PlayerContext p) => p.Kills != 0 || p.Assists != 0 || p.Deaths != 0;

        // No-action edge case: time ran out, no objective, and the winning team recorded zero stats.
        if (winReason == RoundWinReason.Timeout
            && objectiveBombState == BombState.None
            && winners.All(p => !HasAnyStats(p)))
        {
            return (-1, null);
        }

        // Objective priority: defuse/explosion should pick objective player.
        if (winReason == RoundWinReason.Objective && objectivePlayer != null)
        {
            // Planter: bomb exploded — planter always wins MVP.
            if (objectiveBombState == BombState.Exploded)
            {
                return (objectivePlayer.Id, "planting the bomb");
            }

            // Defuse exception: defuser w/ exactly 0 round kills can lose MVP to top-frag teammate.
            if (objectiveBombState == BombState.Defused)
            {
                var defuser = winners.FirstOrDefault(p => p.player != null && p.player == objectivePlayer);
                if (defuser != null && defuser.RoundKills == 0)
                {
                    int maxKills = winners.Max(p => p.RoundKills);
                    if (maxKills > 0)
                        return BreakTies(winners.Where(p => p.RoundKills == maxKills).ToList(), rng);
                }

                return (objectivePlayer.Id, "defusing the bomb");
            }

            return (objectivePlayer.Id, null);
        }

        // Elimination/timeout fallback: most kills this round.
        int bestKills = winners.Max(p => p.RoundKills);
        return BreakTies(winners.Where(p => p.RoundKills == bestKills).ToList(), rng);
    }

    private static (int mvpId, string mvpReason) BreakTies(List<PlayerContext> tiedOnKills, System.Random rng)
    {
        if (tiedOnKills == null || tiedOnKills.Count == 0) return (-1, null);
        if (tiedOnKills.Count == 1) return (tiedOnKills[0].player?.Id ?? -1, "most kills this round");

        // Tiebreak 1: most headshots this round.
        int bestHeadshots = tiedOnKills.Max(p => p.RoundHeadshots);
        if (bestHeadshots > 0)
        {
            var tiedOnHeadshots = tiedOnKills.Where(p => p.RoundHeadshots == bestHeadshots).ToList();
            if (tiedOnHeadshots.Count == 1) return (tiedOnHeadshots[0].player?.Id ?? -1, "most headshots this round");

            // Tiebreak 2: most assists.
            int bestAssists = tiedOnHeadshots.Max(p => p.Assists);
            var tiedOnAssists = tiedOnHeadshots.Where(p => p.Assists == bestAssists).ToList();
            if (tiedOnAssists.Count == 1) return (tiedOnAssists[0].player?.Id ?? -1, "most assists this round");

            int idx = rng.Next(tiedOnAssists.Count);
            return (tiedOnAssists[idx].player?.Id ?? -1, "most kills this round");
        }

        // Tiebreak 2: most assists.
        int bestAssistsTop = tiedOnKills.Max(p => p.Assists);
        var tied = tiedOnKills.Where(p => p.Assists == bestAssistsTop).ToList();
        if (tied.Count == 1) return (tied[0].player?.Id ?? -1, "most assists this round");

        int randomIdx = rng.Next(tied.Count);
        return (tied[randomIdx].player?.Id ?? -1, "most kills this round");
    }
}
