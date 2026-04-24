using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
    public static FieldInfo _reverbSimpleSourceField = AccessTools.Field(typeof(ReverbSimpleSource), "_reverbSource");
    public static FieldInfo _reverbSuperSourceAField = AccessTools.Field(typeof(ReverbSuperSource), "_reverbSourceA");
    public static FieldInfo _reverbSuperSourceBField = AccessTools.Field(typeof(ReverbSuperSource), "_reverbSourceB");
    public static FieldInfo PresetField = AccessTools.Field(typeof(BetterSource), "Preset");


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

    public static void AttachToBetterSource(BetterSource betterSource)
    {
        var source1Cache = GetOrAdd(betterSource.source1);

        if (betterSource is ReverbSimpleSource reverbSimpleSource)
        {
            AudioSource _reverbSource = _reverbSimpleSourceField.GetValue(reverbSimpleSource) as AudioSource;
            var _reverbSourcebCache = GetOrAdd(_reverbSource);
        }
        else if (betterSource is SuperSource superSource)
        {
            var source2Cache = GetOrAdd(superSource.source2);

            if (superSource is ReverbSuperSource reverbSuperSource)
            {
                AudioSource _reverbSuperSourceA = _reverbSuperSourceAField.GetValue(reverbSuperSource) as AudioSource;
                var _reverbSuperSourceACache = GetOrAdd(_reverbSuperSourceA);

                AudioSource _reverbSuperSourceB = _reverbSuperSourceBField.GetValue(reverbSuperSource) as AudioSource;
                var _reverbSuperSourceBCache = GetOrAdd(_reverbSuperSourceB);
            }
        }
    }

    public static SteamSourceData GetOrAdd(AudioSource audioSource)
    {
        if (SteamSourceDict.cache.ContainsKey(audioSource)) return SteamSourceDict.cache[audioSource];

        bool initialSpatialize = audioSource.spatialize;
        float initialBlend = audioSource.spatialBlend;

        var data = new SteamSourceData
        {
            steam = audioSource.gameObject.GetOrAddComponent<SteamAudioSource>(),
            bridge = audioSource.gameObject.GetOrAddComponent<PhononDSPBridge>()
        };

        SteamSourceDict.cache[audioSource] = data;

        SteamAudioSource steamAudio = data.steam;
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

        audioSource.spatialize = initialSpatialize;
        audioSource.spatialBlend = initialBlend;

        return data;
    }

    public static void RouteAudioSource(BetterSource betterSource, AudioClip clip1, bool forceStereo)
    {
        bool shouldBypassSteamAudio = false;
        string bypassReason = "";

        if (forceStereo)
        {
            shouldBypassSteamAudio = true;
            bypassReason = "ForceStereo is True";
        }

        var preset = PresetField.GetValue(betterSource) as AudioGroupPreset;
        if (!shouldBypassSteamAudio && preset != null)
        {
            var type = preset.Type;
            if (type == BetterAudio.AudioSourceGroupType.Nonspatial ||
                type == BetterAudio.AudioSourceGroupType.NonspatialBypass ||
                type == BetterAudio.AudioSourceGroupType.Environment ||
                type == BetterAudio.AudioSourceGroupType.OutEnvironment)
            {
                shouldBypassSteamAudio = true;
                bypassReason = $"Preset is {type}";
            }
        }

        string mixerName = betterSource.source1.outputAudioMixerGroup?.name ?? "";
        if (!shouldBypassSteamAudio)
        {

            if (mixerName.Contains("ClientPlayer") ||
                mixerName.Contains("Ambient") ||
                mixerName.Contains("UI") ||
                mixerName.Contains("Music"))
            {
                shouldBypassSteamAudio = true;
                bypassReason = $"Mixer is {mixerName}";
            }
        }

        // D.Log(mixerName);
        // D.Log(betterSource.source1.spatialize.ToString());
        // D.Log(betterSource.source1.spatialBlend.ToString());
        // D.Log(shouldBypassSteamAudio.ToString());
        // D.Log(bypassReason);
        

        var sources = new List<AudioSource>();

        // Always include source1
        sources.Add(betterSource.source1);

        if (betterSource is ReverbSimpleSource reverbSimpleSource)
        {
            AudioSource reverb = _reverbSimpleSourceField.GetValue(reverbSimpleSource) as AudioSource;
            if (reverb != null) sources.Add(reverb);
        }
        else if (betterSource is SuperSource superSource)
        {
            if (superSource.source2 != null)
                sources.Add(superSource.source2);

            if (superSource is ReverbSuperSource reverbSuperSource)
            {
                AudioSource a = _reverbSuperSourceAField.GetValue(reverbSuperSource) as AudioSource;
                AudioSource b = _reverbSuperSourceBField.GetValue(reverbSuperSource) as AudioSource;

                if (a != null) sources.Add(a);
                if (b != null) sources.Add(b);
            }
        }

        foreach (var src in sources)
        {
            var cache = GetOrAdd(src);
            bool wasBypassed = cache.bridge.IsBypass;

            if (shouldBypassSteamAudio)
            {
                if (!wasBypassed)
                {
                    cache.bridge.IsBypass = true; // turn off harmony patches on audio source, and stop playing via steam audio

                    // restore intended values back to unity
                    src.spatialize = cache.bridge.spatialize;
                    src.spatialBlend = cache.bridge.spatialBlend;
                }
            }
            else
            {
                if (wasBypassed)
                {
                    // The game just wrote fresh values into Native Unity. We need to 
                    // capture them BEFORE turning the patches on!
                    bool currentNativeSpatialize = src.spatialize;
                    float currentNativeBlend = src.spatialBlend;

                    cache.bridge.IsBypass = false; // Turn ON Harmony interceptions

                    // assigning these will now cleanly move them  into cache.bridge and force Native Unity to 0 / false
                    src.spatialize = currentNativeSpatialize;
                    src.spatialBlend = currentNativeBlend;
                }
                // If wasBypassed is already false, the game's prior writes were already intercepted properly. Do nothing!
            }
        }

// #if DEBUG
//         if (shouldBypassSteamAudio)
//             Debugging.Log($"[SteamAudio] Bypassed {clip1?.name} | Reason: {bypassReason}");
//         else
//             Debugging.Log($"[SteamAudio] Playing {clip1?.name}");
// #endif
    }
}