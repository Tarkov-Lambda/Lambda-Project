using System.Collections.Generic;
using System.Linq;
using EFT;
using ifp.arena.bep.Core;
using ifp.arena.shared;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RespawnUtilities
{
    private const int LOS_LAYER_MASK = 18;

    public static Vector3 GetBestSpawnPoint(this PlayerScore respawningPlayer)
    {
        TryGetAllSpawnPointClusters(H.Session.level, respawningPlayer.Faction, 0, out List<SpawnPointCluster> availableSpawnClusters);

        var enemyPositions = GetAllEnemyPositions(respawningPlayer.Faction).ToList();

        IRespawnWeights weights = (H.ActiveRules as IGMRespawnable)?.RespawnWeights;
        if (weights == null) return availableSpawnClusters.FirstOrDefault()?.transform.GetChild(0).position ?? Vector3.zero;

        Vector3 bestSpawnPoint = Vector3.zero;
        float bestSpawnPointRating = float.MinValue;

        foreach (var spawnCluster in availableSpawnClusters)
        {
            foreach (Transform spawnPoint in spawnCluster.transform)
            {
                float rating = RateSpawnPoint(spawnPoint.position, respawningPlayer, enemyPositions, weights);

                // rating += Random.Range(-0.05f, 0.05f);

                if (rating > bestSpawnPointRating)
                {
                    bestSpawnPoint = spawnPoint.position;
                    bestSpawnPointRating = rating;
                }
            }
        }

        return bestSpawnPoint;
    }

    public static float RateSpawnPoint(Vector3 spawnPoint, PlayerScore respawningPlayer, IEnumerable<Vector3> enemyPositions, IRespawnWeights weights)
    {
        float totalScore = 0f;

        float safetyScore = RateSafety(spawnPoint, enemyPositions, weights.MaxEnemyDistanceConsidered);

        // for now return if safety is under 0
        if (safetyScore < 0) return -1000f;

        totalScore += safetyScore * weights.SafetyWeight;

        if (weights.ObjectiveWeight > 0 && H.ActiveRules is IGMSingularActiveObjective singularObjectiveGamemode)
        {
            float objScore = RateObjectiveCloseness(spawnPoint, singularObjectiveGamemode.CurrentObjective, weights.IdealObjectiveDistance);
            totalScore += objScore * weights.ObjectiveWeight;
        }

        if (weights.TeamCohesionWeight > 0 && H.ActiveRules is IGMTeam)
        {
            float teamScore = RateTeamCohesion(spawnPoint, respawningPlayer);
            totalScore += teamScore * weights.TeamCohesionWeight;
        }

        return totalScore;
    }

    public static float RateSafety(Vector3 spawnPoint, IEnumerable<Vector3> enemyPositions, float maxDistanceConsidered)
    {
        float closestEnemyDist = float.MaxValue;
        // TODO: REFACTOR
        // cache all players FPS Camera
        Vector3 spawnHeadPos = spawnPoint + Vector3.up * 1.5f;

        foreach (Vector3 enemyPos in enemyPositions)
        {
            Vector3 enemyHeadPos = enemyPos + Vector3.up * 1.5f;
            float dist = Vector3.Distance(spawnPoint, enemyPos);

            if (dist < closestEnemyDist) closestEnemyDist = dist;

            // if enemy is way too close
            if (dist < 15f) return -100f;

            // LINE OF SIGHT CHECK
            // If the enemy is within a relevant distance, check if they can see the spawn
            if (dist < 80f)
            {
                Vector3 dirToSpawn = (spawnHeadPos - enemyHeadPos).normalized;

                if (!Physics.Raycast(enemyHeadPos, dirToSpawn, dist, LOS_LAYER_MASK))
                {
                    return -500f;
                }
            }
        }

        // Normalize distance score (farther = better, up to maxDistanceConsidered)
        closestEnemyDist = Mathf.Clamp(closestEnemyDist, 0, maxDistanceConsidered);
        return closestEnemyDist / maxDistanceConsidered;
    }

    public static float RateObjectiveCloseness(Vector3 spawnPoint, ILambdaObjective objective, float idealDistance)
    {
        float actualDist = Vector3.Distance(spawnPoint, objective.Center);

        // penalize being too close and penalize being too far.
        float diff = Mathf.Abs(actualDist - idealDistance);

        // Max penalty distance (e.g., 50 meters away from the ideal spot = score of 0)
        float maxDiff = 50f;

        float score = 1f - (diff / maxDiff);
        return Mathf.Clamp01(score);
    }

    public static float RateTeamCohesion(Vector3 spawnPoint, PlayerScore respawningPlayer)
    {
        List<PlayerScore> teammates = H.Session.GetPlayerScoresFromFaction(respawningPlayer.Faction);
        if (teammates.Count == 0) return 0.5f;

        float averageDist = 0f;
        int aliveTeammates = 0;

        foreach (var tm in teammates)
        {
            if (tm.IsAlive && tm.player != respawningPlayer.player)
            {
                averageDist += Vector3.Distance(spawnPoint, tm.player.Position);
                aliveTeammates++;
            }
        }

        if (aliveTeammates == 0) return 0.5f;
        averageDist /= aliveTeammates;

        // Ideal teammate distance is roughly 15-25 meters. Too close = nade bait.
        float diff = Mathf.Abs(averageDist - 20f);
        return Mathf.Clamp01(1f - (diff / 40f));
    }

    public static float DistanceToObjective(Vector3 spawnPoint, ILambdaObjective objectivePositions)
    {
        return (spawnPoint - objectivePositions.Center).sqrMagnitude;
    }

    public static IEnumerable<Vector3> GetAllEnemyPositions(Faction ownFaction)
    {
        Faction enemyFaction;

        if (H.ActiveRules is IGMTeam)
        {
            enemyFaction = ownFaction == Faction.CT ? Faction.T : Faction.CT;
        }
        else
        {
            enemyFaction = Faction.None;
        }

        var enemyPlayers = H.Session.GetPlayersFromFaction(enemyFaction);

        foreach (Player enemyPlayer in enemyPlayers)
        {
            if (!H.IsHeadless && enemyPlayer.IsYourPlayer)
                continue;

            yield return enemyPlayer.GetPosition();
        }
    }

    public static bool TryGetAllSpawnPointClusters(string sceneName, Faction faction, int pair, out List<SpawnPointCluster> newPos)
    {
        newPos = new();

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene == null)
        {
            D.LogError($"Trying to find spawn points in a scene that doesn't exist");
            return false;
        }

        if (!scene.isLoaded)
        {
            D.LogError($"Trying to find spawn points in a scene that is not LOADED!"); // bro what the fuck are we logging
            return false;
        }

        List<SpawnPointCluster> allSpawnPoints = new List<SpawnPointCluster>();

        foreach (var rootGameObject in scene.GetRootGameObjects())
        {
            SpawnPointCluster[] sPoints = rootGameObject.GetComponentsInChildren<SpawnPointCluster>();
            if (sPoints.Length > 0)
            {
                allSpawnPoints.AddRange(sPoints);
            }
        }

        if (allSpawnPoints.Count == 0)
        {
            D.LogError($"level {sceneName} contains no spawn points");
            return false;
        }

        var allSpawnPointClusters = allSpawnPoints.FindAll(spawnPointCluster => spawnPointCluster.faction == faction && spawnPointCluster?.pairId == pair);

        newPos = allSpawnPointClusters;
        return true;
    }

    public static float GetSqrMagnitudeBetweenTwoPlayers(Player player1, Player player2)
    {
        Vector3 pos1 = player1.GetPosition();
        Vector3 pos2 = player2.GetPosition();

        return (pos1 - pos2).sqrMagnitude;
    }

    public static float DistanceToClosestEnemy(Vector3 spawnPoint, IEnumerable<Vector3> enemyPositions)
    {
        float sqrMagnitudeToClosestEnemy = float.MaxValue;

        foreach (Vector3 enemyPos in enemyPositions)
        {
            float sqrDist = (spawnPoint - enemyPos).sqrMagnitude;

            if (sqrDist < sqrMagnitudeToClosestEnemy)
            {
                sqrMagnitudeToClosestEnemy = sqrDist;
            }
        }

        return Mathf.Sqrt(sqrMagnitudeToClosestEnemy);
    }
}