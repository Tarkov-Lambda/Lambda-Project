using Comfort.Common;
using EFT;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.Core.Dying
{
    public class Teleporter
    {
        static public Vector3 newPos;

        // Currently the teleport decides for itself where to teleport the player which is suboptimal in future but will work for now
        static public void Teleport(Player player)
        {
            try
            {
                Faction faction;
                PlayerScore playerScore = H.GetPlayerScore(player.Id);

                // if (H.session.roundState == MatchState.None || (playerScore != null && !playerScore.isAlive))
                // {
                //     faction = Faction.Lobby;
                // }
                // else
                // {
                //     faction = Plugin.PrefferedFaction.Value;
                // }

                newPos = GetNewPosition(playerScore.faction);
                // player.Position = newPos;
                player.Teleport(newPos);
            }
            catch (Exception ex)
            {
                // H.Notify("ERROR: Can't teleport");
                // Plugin.Logger.LogError(ex);
            }
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
