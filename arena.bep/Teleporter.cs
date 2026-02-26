using EFT;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.bep
{
    public class Teleporter
    {
        static public Vector3 newPos;

        static public void Teleport(Player player)
        {
            var spawnPoints = GameObject.FindGameObjectWithTag("Respawn");
            
            if(spawnPoints == null)
            {
                Plugin.Logger.LogInfo("ne mogu naiti blya");
                Plugin.Logger.LogInfo(spawnPoints);
            }
            //newPos = spawnPoints.GetPositions()[1];

            player.Teleport(newPos);
        }

    }
}
