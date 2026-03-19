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

namespace ifp.arena.bep.Core.Dying
{
    public class Teleporter
    {
        static public Vector3 newPos;

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
            else if (pScore.isAlive)
            {
                targetMap = H.Session.mapName;
                targetFaction = pScore.faction;
            }
            else
            {
                targetMap = "lobby";
                targetFaction = Faction.None;
            }

            if (!TryGetNewPosition(targetMap, targetFaction, out Vector3 nextPlayerPosition))
            {
                H.LogError($"Can't find a teleport position in {targetMap.ToLower()}");
                return;
            }

            player.Teleport(nextPlayerPosition);
        }

        public static bool TryGetNewPosition(string sceneName, Faction faction, out Vector3 newPos)
        {
            Scene s = SceneManager.GetSceneByName(sceneName);
            if (s == null) H.LogError($"Trying to find spawn points in a scene that doesn't exist");

            GameObject[] gObjects = s.GetRootGameObjects();

            SpawnPoints[] allSpawnPoints = [];
            foreach (GameObject gObject in gObjects)
            {
                SpawnPoints[] sPoints = UnityEngine.Object.FindObjectsByType<SpawnPoints>(FindObjectsSortMode.None);
                if (sPoints.Length > 0)
                {
                    allSpawnPoints = sPoints;
                    break;
                }
            }

            if (allSpawnPoints.Length == 0)
            {
                newPos = Vector3.zero;
                return false;
            }

            var currentSpawnPoints = allSpawnPoints.First(spawnPoint => spawnPoint.faction == faction);

            var list = new List<Vector3>();
            foreach (Transform transform in currentSpawnPoints.transform)
            {
                list.Add(transform.position);
            }

            newPos = list.ToArray().RandomElement();
            return true;
        }

    }
}
