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
    static FieldInfo _reverbSourceField = AccessTools.Field(typeof(ReverbSimpleSource), "_reverbSource");

    public static async void Initialize()
    {
        List<BetterSource> betterSources = H.GameWorld?.gameObject.GetComponentsInChildren<BetterSource>(true).ToList();

        foreach (Player player in H.AllPlayers)
        {
            if (player.IsYourPlayer) continue;

            foreach (BetterSource betterSource in player.gameObject.GetComponentsInChildren<BetterSource>())
            {
                AttachToBetterSource(betterSource);
            }
        }

        foreach (BetterSource betterSource in betterSources) AttachToBetterSource(betterSource);
    }

    private static void AttachToBetterSource(BetterSource betterSource)
    {
        var source1Cache = GetOrAdd(betterSource.source1);
        source1Cache.bridge.IsBypass = false;

        if (betterSource is ReverbSimpleSource reverbSimpleSource)
        {
            AudioSource reverbSource = _reverbSourceField.GetValue(reverbSimpleSource) as AudioSource;
            var reverbCache = GetOrAdd(reverbSource);
            reverbCache.bridge.IsBypass = false;
        }
        else if (betterSource is SuperSource superSource)
        {
            var source2Cache = GetOrAdd(superSource.source2);
            source2Cache.bridge.IsBypass = false;
        }
    }

    private static SteamSourceData GetOrAdd(AudioSource audioSource)
    {
        // Reinitialize on Hot Reload
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

        steamAudio.reflections = false;

        // turn off internal spatializer
        audioSource.spatialize = false;
        var cachedSpatialBlend = audioSource.spatialBlend;
        audioSource.spatialBlend = 0.1268321f;              // force the spatialBlend to change
        audioSource.spatialBlend = cachedSpatialBlend;      // retrieve the cached real value, send it to PhononDSPBridge through the harmony patch


        return SteamSourceDict.cache[audioSource];
    }
}