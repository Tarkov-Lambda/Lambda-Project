using Comfort.Common;
using EFT;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using RootMotion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.Core.Dying
{
    public class Teleporter
    {
        static public Vector3 newPos;

        static public void Teleport(Player player)
        {
            var faction = BaseGameMode.Instance.session.currentGameMode == GameModes.FFA;
            newPos = GetNewPosition( Plugin.PrefferedFaction.Value);
            player.Teleport(newPos);
        }

        public static Vector3 GetNewPosition(Faction faction)
        {
            SpawnPoints[] allSpawnPoints = UnityEngine.Object.FindObjectsByType<SpawnPoints>(FindObjectsSortMode.None);
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
