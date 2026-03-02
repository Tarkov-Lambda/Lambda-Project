using Comfort.Common;
using EFT;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
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
            Faction faction;
            SessionInfo session = BaseGameMode.Instance.session;
            PlayerScore playerScore = session.scoreboard.FirstOrDefault(p => p.Value.player == Singleton<GameWorld>.Instance.MainPlayer).Value;
            if (session.roundState == RoundState.None || (playerScore != null && !playerScore.isAlive))
            {
                faction = Faction.Lobby;
            } else
            {
                faction = Plugin.PrefferedFaction.Value;
            }

                newPos = GetNewPosition(Plugin.PrefferedFaction.Value);
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
