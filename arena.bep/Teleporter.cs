using EFT;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep
{
    public class Teleporter
    {
        static public Vector3 newPos;

        static public void Teleport(Player player)
        {
            newPos = GetNewPosition(Plugin.PrefferedFaction.Value);
            player.Teleport(newPos);
        }

        public static Vector3 GetNewPosition(Faction faction)
        {
            SpawnPoints[] allSpawnPoints = GameObject.FindObjectsByType<SpawnPoints>(FindObjectsSortMode.None);
            var currentSpawnPoints = allSpawnPoints.First(spawnPoint => spawnPoint.faction == faction);

            var list = new List<Vector3>();
            foreach (Transform transform in currentSpawnPoints.transform)
            {
                list.Add(transform.position);
            }

            return list.ToArray().RandomElement();
        }

    }
}
