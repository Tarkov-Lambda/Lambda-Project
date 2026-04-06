using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Audio.ReverbSubsystem;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.shared;
using SteamAudio;
using UnityEngine;

public struct SteamSourceData
{
    public SteamAudioSource steam;
    public PhononDSPBridge bridge;

}

internal class SteamSourceDict
{
    public static readonly Dictionary<AudioSource, SteamSourceData> cache = new();
}


public static class SteamAudioSourceAttacher
{

    public static async void Initialize()
    {
        List<ReverbSimpleSource> reverbSimpleSources = H.GameWorld?.gameObject.GetComponentsInChildren<ReverbSimpleSource>(true).ToList();
        List<BetterSource> playerBetterSources = new();

        foreach (Player player in H.AllPlayers)
        {
            var playerAudioSources = player.gameObject.GetComponentsInChildren<BetterSource>();
            playerBetterSources.AddRange(playerAudioSources);
        }

        FieldInfo _reverbSourceField = AccessTools.Field(typeof(ReverbSimpleSource), "_reverbSource");

        // Separate so I can do some shit later
        foreach (BetterSource betterSource in playerBetterSources)
        {
            Player player = betterSource.GetComponentInParent<Player>();

            var source1Cache = GetOrAdd(betterSource.source1);
            if (player.IsYourPlayer) betterSource.source1.spatialBlend = 0f;

            if (betterSource is ReverbSimpleSource reverbSimpleSource)
            {
                AudioSource reverbSource = _reverbSourceField.GetValue(reverbSimpleSource) as AudioSource;

                var reverbCache = GetOrAdd(reverbSource);

                if (player.IsYourPlayer) reverbCache.bridge.IsBypass = true;
            }
            else if (betterSource is ReverbSuperSource reverbSuperSource)
            {
                D.Log("is ReverbSuperSource");
            }
            else if (betterSource is SuperSource superSource)
            {
                D.Log("is superSource");
            }


            // source1Cache.steam.distanceAttenuation = true;
            // reverbCache.steam.distanceAttenuation = true;
            // MonoBehaviourSingleton<AudioSourceWorldDebug>.Instance.audioSources.Add(reverbSimpleSource.source1);
        }

        foreach (ReverbSimpleSource reverbSimpleSource in reverbSimpleSources)
        {
            AudioSource reverbSource = _reverbSourceField.GetValue(reverbSimpleSource) as AudioSource;

            var source1Cache = GetOrAdd(reverbSimpleSource.source1);
            var reverbCache = GetOrAdd(reverbSource);

            source1Cache.bridge.IsBypass = false;
            reverbCache.bridge.IsBypass = false;
        }


        List<SuperSource> superSources = H.GameWorld?.gameObject.GetComponentsInChildren<SuperSource>(true).ToList();
        foreach (SuperSource superSource in superSources)
        {
            var source1Cache = GetOrAdd(superSource.source1);
            var source2Cache = GetOrAdd(superSource.source2);

            source1Cache.bridge.IsBypass = false;
            source2Cache.bridge.IsBypass = false;

            // source1Cache.steam.distanceAttenuation = true;
            // source2Cache.steam.distanceAttenuation = true;
        }
    }

    private static SteamSourceData GetOrAdd(AudioSource audioSource)
    {
        // if (SteamSourceDict.cache.ContainsKey(audioSource))
        // {
        //     Object.Destroy(SteamSourceDict.cache[audioSource].bridge);
        //     Object.Destroy(SteamSourceDict.cache[audioSource].steam);
        //     SteamSourceDict.cache.Remove(audioSource);
        // }


        if (SteamSourceDict.cache.ContainsKey(audioSource)) return SteamSourceDict.cache[audioSource];

        SteamSourceDict.cache[audioSource] = new SteamSourceData
        {
            steam = audioSource.gameObject.GetOrAddComponent<SteamAudioSource>(),
            bridge = audioSource.gameObject.GetOrAddComponent<PhononDSPBridge>()
        };

        SteamAudioSource steamAudio = SteamSourceDict.cache[audioSource].steam;
        steamAudio.occlusion = true;
        steamAudio.transmission = true;

        steamAudio.distanceAttenuation = true;
        steamAudio.distanceAttenuationInput = DistanceAttenuationInput.CurveDriven;
        steamAudio.distanceAttenuationValue = 1f;

        steamAudio.airAbsorption = false;
        steamAudio.airAbsorptionInput = AirAbsorptionInput.SimulationDefined;

        steamAudio.occlusionType = OcclusionType.Raycast;
        steamAudio.occlusionRadius = 1.4f;
        steamAudio.occlusionSamples = 8;
        steamAudio.transmissionType = TransmissionType.FrequencyDependent;
        steamAudio.transmissionInput = TransmissionInput.UserDefined;
        steamAudio.transmissionHigh = 0.2f;
        steamAudio.transmissionMid = 0.4f;
        steamAudio.transmissionLow = 0.5f;

        audioSource.spatialize = false;
        var cachedSpatialBlend = audioSource.spatialBlend;
        audioSource.spatialBlend = 0.1268321f; // forcing it to change
        audioSource.spatialBlend = cachedSpatialBlend;


        return SteamSourceDict.cache[audioSource];
    }
}