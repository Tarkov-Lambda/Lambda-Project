using System.Collections.Generic;
using System.Linq;
using EFT;
using ifp.arena.bep.Core;
using ifp.arena.shared;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RespawnUtilities
{
    public static Vector3 GetBestSpawnPoint(this PlayerScore respawningPlayer)
    {
        TryGetAllSpawnPointClusters(H.Session.mapName, respawningPlayer.Faction, 0, out List<SpawnPointCluster> availableSpawnClusters);

        var enemyPositions = GetAllEnemyPositions(respawningPlayer.Faction).ToList();

        Vector3 bestSpawnPoint = Vector3.zero;
        float bestSpawnPointRating = 0f;

        foreach (var spawnCluster in availableSpawnClusters)
        {
            foreach (var spawnPoint in spawnCluster.transform)
            {
                GameObject spawnPointGO = spawnPoint as GameObject;

                var rating = RateSpawnPoint(spawnPointGO.transform.position, respawningPlayer, enemyPositions);

                if (rating > bestSpawnPointRating)
                {
                    bestSpawnPoint = spawnPointGO.transform.position;
                    bestSpawnPointRating = rating;
                }
            }
        }

        return bestSpawnPoint;
    }

    public static float RateSpawnPoint(Vector3 spawnPoint, PlayerScore respawningPlayer, IEnumerable<Vector3> enemyPositions)
    {
        float rating = 0f;

        float safetyRating = RateSafety(spawnPoint, respawningPlayer, enemyPositions);

        float objectiveRating;
        if (H.ActiveRules is IObjectiveBased objectiveGamemode)
        {
            if (objectiveGamemode is ISingularObjectiveBased singularObjectiveGamemode)
            {
                objectiveRating = RateObjectiveCloseness(spawnPoint, respawningPlayer, singularObjectiveGamemode.CurrentObjective);
            }
        }

        float teamRating;
        if (H.ActiveRules is ITeamBased teamGamemode)
        {
            teamRating = RateTeamCohesion(spawnPoint, respawningPlayer);
        }

        return 10f;
    }

    public static float RateSafety(Vector3 spawnPoint, PlayerScore respawningPlayer, IEnumerable<Vector3> enemyPositions)
    {
        float enemyDistance = DistanceToClosestEnemy(spawnPoint, enemyPositions);

        // line of sight with cone

        return 10f;
    }

    public static float RateObjectiveCloseness(Vector3 spawnPoint, PlayerScore respawningPlayer, ILambdaObjective objectivePositions)
    {
        float distance = DistanceToObjective(spawnPoint, objectivePositions);

        return 10f;
    }

    public static float RateTeamCohesion(Vector3 spawnPoint, PlayerScore respawningPlayer)
    {
        List<PlayerScore> teammates = H.Session.GetPlayerScoresFromFaction(respawningPlayer.Faction);

        return 10f;
    }

    public static float DistanceToObjective(Vector3 spawnPoint, ILambdaObjective objectivePositions)
    {
        return (spawnPoint - objectivePositions.Center).sqrMagnitude;
    }

    public static IEnumerable<Vector3> GetAllEnemyPositions(Faction ownFaction)
    {
        Faction enemyFaction;

        if (H.ActiveRules is ITeamBased)
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

        var allSpawnPointClusters = allSpawnPoints.FindAll(spawnPointCluster => spawnPointCluster.faction == faction && spawnPointCluster.pairId == pair);

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