using Comfort.Common;
using EFT;
using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ifp.arena.bep.Core.Dying;

public class Teleporter
{
    // Currently the teleport decides for itself where to teleport the player which is suboptimal in future but will work for now
    static public void Teleport(Player player, string mapName = "", Faction faction = Faction.None)
    {
        PlayerScore pScore = H.GetPlayerScore(player.Id);

        string targetMap;
        Faction targetFaction;

        if (!string.IsNullOrEmpty(mapName))
        {
            targetMap = mapName;
            targetFaction = faction;
        }
        else if (pScore.IsAlive)
        {
            targetMap = H.Session.mapName;
            targetFaction = pScore.Faction;
        }
        else
        {
            targetMap = "lobby";
            targetFaction = Faction.None;
        }

        if (!TryGetNewPosition(targetMap, targetFaction, out Vector3 nextPlayerPosition))
        {
            D.LogError($"Can't find a teleport position in {targetMap.ToLower()}");
            return;
        }

        D.Log($"Teleporting {player.Profile.Nickname}");
        player.Teleport(nextPlayerPosition);
    }

    public static bool TryGetNewPosition(string sceneName, Faction faction, out Vector3 newPos)
    {
        newPos = Vector3.zero;

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene == null)
        {
            D.LogError($"Trying to find spawn points in a scene that doesn't exist");
            return false;
        }

        if (!scene.isLoaded)
        {
            D.LogError($"Trying to find spawn points in a scene that is not LOADED! fukc you"); // bro what the fuck are we logging
            return false;
        }

        List<SpawnPoints> allSpawnPoints = new List<SpawnPoints>();

        foreach (var rootGameObject in scene.GetRootGameObjects())
        {
            SpawnPoints[] sPoints = rootGameObject.GetComponentsInChildren<SpawnPoints>();
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

        var currentSpawnPoints = allSpawnPoints.FirstOrDefault(spawnPoint => spawnPoint.faction == faction);
        if (currentSpawnPoints == null)
        {
            D.LogError($"Can't find spawn point for {faction} faction");
            return false;
        }

        var list = new List<Vector3>();
        foreach (Transform transform in currentSpawnPoints.transform)
        {
            list.Add(transform.position);
        }

        newPos = list.ToArray().RandomElement();
        return true;
    }

}
