using Comfort.Common;
using EFT;
using Lambda.Core.Networking;
using ifp.arena.shared;
using System.Collections.Generic;
using UnityEngine;

namespace Lambda.Core.Main.Dying;

public class Teleporter
{
    // Currently the teleport decides for itself where to teleport the player which is suboptimal in future but will work for now
    static public void Teleport(Player player, string mapName, Faction faction, int pair = 0)
    {
        string targetMap = mapName;
        Faction targetFaction = faction;

        // string targetMap;
        // Faction targetFaction;

        // if (!string.IsNullOrEmpty(mapName))
        // {
        //     targetMap = mapName;
        //     targetFaction = faction;
        // }
        // else if (pScore.IsAlive)
        // {
        //     targetMap = H.Session.mapName;
        //     targetFaction = pScore.Faction;
        // }
        // else
        // {
        //     targetMap = "lobby";
        //     targetFaction = Faction.None;
        // }

        if (!TryGetNewPosition(targetMap, targetFaction, pair, out Transform nextPlayerPosition))
        {
            D.LogError($"Can't find a teleport position in {targetMap.ToLower()}");
            return;
        }

        D.Log($"Teleporting {player.Profile.Nickname}");


        var tpPacket = new DictateTeleportPacket
        {
            Player = player,
            position = nextPlayerPosition.position,
            rotation = nextPlayerPosition.rotation
        };

        Singleton<DictateTeleportPacketWarden>.Instance.Apply(tpPacket);
    }

    public static bool TryGetNewPosition(string sceneName, Faction faction, int pair, out Transform newPos)
    {
        newPos = new Transform();

        if (!RespawnUtilities.TryGetAllSpawnPointClusters(sceneName, faction, pair, out List<SpawnPointCluster> spawnPoints))
        {
            return false;
        }

        newPos = spawnPoints.RandomElement().GetRandomSpawn();
        return true;
    }
}
