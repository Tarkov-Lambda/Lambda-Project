using Comfort.Common;
using EFT;
using Lambda.Core.Networking;
using Lambda.Shared;
using System.Collections.Generic;
using UnityEngine;

namespace Lambda.Core.Main;

public class Teleporter
{
    static public void Teleport(Player player, string mapName, Faction faction, int pair = 0)
    {
        string targetMap = mapName;
        Faction targetFaction = faction;

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
        newPos = null;

        if (!RespawnUtilities.TryGetAllSpawnPointClusters(sceneName, faction, pair, out List<SpawnPointCluster> spawnPoints))
        {
            return false;
        }

        newPos = spawnPoints.RandomElement().GetRandomSpawn();
        return newPos != null;
    }
}
