using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using ifp.arena.shared;
using UnityEngine;

namespace ifp.arena.bep.Core;

public class HardpointZoneManager : Singleton<HardpointZoneManager>, IDisposable
{
    private readonly Dictionary<HardpointZone, HashSet<int>> _zonePlayers = new();

    public Dictionary<int, HardpointZone> NetIdtoZone { get; private set; } = new();

    public HardpointZoneManager()
    {
        MapLoadEvent.OnSuccessfulLoad += CreateZoneCache;

        MapLoadEvent.OnBeginUnload += ClearZoneCache;

        if (!H.IsServer)
        {
            HardpointZone.onPlayerEnterLadder += OnTriggerEnter;
            HardpointZone.onPlayerExitLadder += OnTriggerExit;
        }
    }

    public void Dispose()
    {
        MapLoadEvent.OnSuccessfulLoad -= CreateZoneCache;

        MapLoadEvent.OnBeginUnload -= ClearZoneCache;

        if (!H.IsServer)
        {
            HardpointZone.onPlayerEnterLadder -= OnTriggerEnter;
            HardpointZone.onPlayerExitLadder -= OnTriggerExit;
        }

        Release(this);
    }

    private void CreateZoneCache()
    {
        NetIdtoZone = new();

        var zones = UnityEngine.Object.FindObjectsByType<HardpointZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var zone in zones)
        {
            NetIdtoZone[zone.NetId] = zone;
        }
    }


    private void ClearZoneCache()
    {
        NetIdtoZone = new();
    }

    private void CheckZoneOwnership()
    {
        foreach (var kvp in _zonePlayers)
        {
            HardpointZone zone = kvp.Key;
            HashSet<int> playerIds = kvp.Value;

            var cachedZoneOwnership = zone.ZoneOwnership;

            if (playerIds == null || playerIds.Count == 0)
            {
                zone.ChangeOwnership(ZoneOwnership.None);
                continue;
            }

            var factionScores = new Dictionary<Faction, int>();

            foreach (int playerId in playerIds)
            {
                Player player = H.GetPlayer(playerId);
                if (player == null) continue;

                Faction faction = player.GetScore().Faction;

                if (!factionScores.ContainsKey(faction)) factionScores[faction] = 0;

                factionScores[faction]++;
            }

            Faction? leadingFaction = null;
            int highestScore = 0;
            bool contested = false;

            foreach (var entry in factionScores)
            {
                if (entry.Value > highestScore)
                {
                    highestScore = entry.Value;
                    leadingFaction = entry.Key;
                    contested = false;
                }
                else if (entry.Value == highestScore)
                {
                    contested = true;
                }
            }

            if (contested || leadingFaction == null)
            {
                zone.ChangeOwnership(ZoneOwnership.Draw);
            }
            else
            {
                zone.ChangeOwnership(leadingFaction.Value == Faction.CT ? ZoneOwnership.CT : ZoneOwnership.T);
            }

            if (zone.ZoneOwnership != cachedZoneOwnership)
            {

            }
        }
    }

    private void OnTriggerEnter(HardpointEventPayload hardpointEvent)
    {
        Player player = hardpointEvent.other.GetComponentInParent<Player>();
        if (player == null) return;

        var zone = hardpointEvent.hardpoint;
        int playerId = player.Id;

        if (!_zonePlayers.TryGetValue(zone, out var set))
        {
            set = new HashSet<int>();
            _zonePlayers[zone] = set;
        }

        set.Add(playerId);

        zone.playerIdsInZone = new List<int>(set);

        CheckZoneOwnership();
    }

    private void OnTriggerExit(HardpointEventPayload hardpointEvent)
    {
        Player player = hardpointEvent.other.GetComponentInParent<Player>();
        if (player == null) return;

        var zone = hardpointEvent.hardpoint;
        int playerId = player.Id;

        if (_zonePlayers.TryGetValue(zone, out var set))
        {
            set.Remove(playerId);

            zone.playerIdsInZone = new List<int>(set);
        }

        CheckZoneOwnership();
    }
}