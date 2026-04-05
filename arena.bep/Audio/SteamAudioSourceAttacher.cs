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
        List<ReverbSimpleSource> playerReverbSimpleSources = H.GameWorld?.gameObject.GetComponentsInChildren<ReverbSimpleSource>(true).ToList();

        foreach (Player player in H.AllPlayers)
        {
            if (player.IsYourPlayer) continue; // main player avoiding this is lowkey nicer
            var playerAudioSources = player.gameObject.GetComponentsInChildren<ReverbSimpleSource>();
            playerReverbSimpleSources.AddRange(playerAudioSources);
        }

        FieldInfo _reverbSourceField = AccessTools.Field(typeof(ReverbSimpleSource), "_reverbSource");

        // Separate so I can do some shit later
        foreach (ReverbSimpleSource reverbSimpleSource in playerReverbSimpleSources)
        {
            AudioSource reverbSource = _reverbSourceField.GetValue(reverbSimpleSource) as AudioSource;

            var source1Cache = GetOrAdd(reverbSimpleSource.source1);
            var reverbCache = GetOrAdd(reverbSource);

            source1Cache.bridge.IsBypass = false;
            reverbCache.bridge.IsBypass = true;

            MonoBehaviourSingleton<AudioSourceWorldDebug>.Instance.audioSources.Add(reverbSimpleSource.source1);
        }

        foreach (ReverbSimpleSource reverbSimpleSource in reverbSimpleSources)
        {
            AudioSource reverbSource = _reverbSourceField.GetValue(reverbSimpleSource) as AudioSource;

            var source1Cache = GetOrAdd(reverbSimpleSource.source1);
            var reverbCache = GetOrAdd(reverbSource);

            source1Cache.bridge.IsBypass = false;
            reverbCache.bridge.IsBypass = true;
        }


        List<SuperSource> superSources = H.GameWorld?.gameObject.GetComponentsInChildren<SuperSource>(true).ToList();
        foreach (SuperSource superSource in superSources)
        {
            var source1Cache = GetOrAdd(superSource.source1);
            source1Cache.bridge.IsBypass = false;

            var source2Cache = GetOrAdd(superSource.source2);
            source2Cache.bridge.IsBypass = true;
        }
    }

    private static SteamSourceData GetOrAdd(AudioSource audioSource)
    {
        // if (SteamSourceDict.cache.ContainsKey(audioSource)) SteamSourceDict.cache.Remove(audioSource);

        // if (SteamSourceDict.cache.ContainsKey(audioSource)) return SteamSourceDict.cache[audioSource];

        SteamSourceDict.cache[audioSource] = new SteamSourceData
        {
            steam = audioSource.gameObject.GetOrAddComponent<SteamAudioSource>(),
            bridge = audioSource.gameObject.GetOrAddComponent<PhononDSPBridge>()
        };

        SteamAudioSource steamAudio = SteamSourceDict.cache[audioSource].steam;
        steamAudio.occlusion = true;
        steamAudio.transmission = true;

        steamAudio.distanceAttenuation = false;
        steamAudio.distanceAttenuationInput = DistanceAttenuationInput.CurveDriven;
        steamAudio.distanceAttenuationValue = 1f;

        steamAudio.airAbsorption = true;
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
        audioSource.spatialBlend = audioSource.spatialBlend;

        return SteamSourceDict.cache[audioSource];
    }
}