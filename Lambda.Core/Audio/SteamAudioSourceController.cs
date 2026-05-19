using System.Collections.Generic;
using Audio.ReverbSubsystem;
using EFT;
using HarmonyLib;
using Lambda.Audio;
using SteamAudio;
using UnityEngine;
using UnityEngine.Audio;

public struct SteamSourceData
{
    public SteamAudioSource steam;
    public IProxiedAudioSource bridge;
}

public static class SteamAudioSourceController
{
    public static AccessTools.FieldRef<BetterSource, AudioGroupPreset> PresetRef = AccessTools.FieldRefAccess<BetterSource, AudioGroupPreset>("Preset");
    public static AccessTools.FieldRef<BetterSource, bool> ForceStereoRef = AccessTools.FieldRefAccess<BetterSource, bool>("_forceStereo");
    public static AccessTools.FieldRef<ReverbSimpleSource, AudioSource> ReverbSimpleSourceFieldRef = AccessTools.FieldRefAccess<ReverbSimpleSource, AudioSource>("_reverbSource");
    public static AccessTools.FieldRef<ReverbSuperSource, AudioSource> ReverbSuperSourceAFieldRef = AccessTools.FieldRefAccess<ReverbSuperSource, AudioSource>("_reverbSourceA");
    public static AccessTools.FieldRef<ReverbSuperSource, AudioSource> ReverbSuperSourceBFieldRef = AccessTools.FieldRefAccess<ReverbSuperSource, AudioSource>("_reverbSourceB");

    // Selection of mixers we do not route through steam audio
    private static readonly Dictionary<AudioMixerGroup, bool> MixerBypassCache = new();

    public static readonly Dictionary<AudioSource, SteamSourceData> cache = new();

    public static SteamSourceData GetOrAdd(AudioSource audioSource)
    {
        if (cache.TryGetValue(audioSource, out var data))
            return data;

        // Save initial Unity spatial properties before we enable AudioSource patches for:
        // get/set_spatialize
        // get/set_spatialBlend
        // to completely proxy the original fields and give control to PhononDSPBridge (unless we decide otherwise below)
        bool initialSpatialize = audioSource.spatialize;
        float initialBlend = audioSource.spatialBlend;

        data = new SteamSourceData
        {
            steam = audioSource.gameObject.GetOrAddComponent<SteamAudioSource>(),
            bridge = audioSource.gameObject.GetOrAddComponent<PhononDSPBridge>()
        };

        cache[audioSource] = data;

        SteamAudioSource steamAudio = data.steam;

        steamAudio.occlusion = true;
        steamAudio.transmission = true;

        steamAudio.distanceAttenuation = true;
        steamAudio.distanceAttenuationInput = DistanceAttenuationInput.PhysicsBased;
        steamAudio.distanceAttenuationValue = 1f;

        steamAudio.airAbsorption = true;
        steamAudio.airAbsorptionInput = AirAbsorptionInput.SimulationDefined;

        steamAudio.occlusionType = OcclusionType.Raycast;
        steamAudio.occlusionRadius = 1.4f;
        steamAudio.occlusionSamples = 8;

        steamAudio.transmissionType = TransmissionType.FrequencyDependent;
        steamAudio.transmissionInput = TransmissionInput.UserDefined;

        steamAudio.transmissionHigh = GameplayVariables.vars.transmissionHigh;
        steamAudio.transmissionMid = GameplayVariables.vars.transmissionMid;
        steamAudio.transmissionLow = GameplayVariables.vars.transmissionLow;

        audioSource.spatialize = initialSpatialize;
        audioSource.spatialBlend = initialBlend;

        return data;
    }

    // it works idgaf
    private static bool IsMixerBypassed(AudioMixerGroup mixer)
    {
        if (mixer == null) return false;

        if (MixerBypassCache.TryGetValue(mixer, out bool bypassed))
            return bypassed;

        string mixerName = mixer.name;
        bypassed =
                   mixerName.Contains("Ambient") ||
                   mixerName.Contains("UI") ||
                   mixerName.Contains("Music");

        MixerBypassCache[mixer] = bypassed;
        return bypassed;
    }

    public static void RouteBetterSource(BetterSource betterSource, bool? forceStereoOverride = null)
    {
        if (betterSource == null || betterSource.source1 == null) return;

        bool forceStereo = forceStereoOverride ?? ForceStereoRef(betterSource);
        bool shouldBypassSteamAudio = false;

        var preset = PresetRef(betterSource);
        if (preset != null)
        {
            var type = preset.Type;
            if (type
                is BetterAudio.AudioSourceGroupType.Nonspatial
                or BetterAudio.AudioSourceGroupType.NonspatialBypass
                or BetterAudio.AudioSourceGroupType.Environment
                or BetterAudio.AudioSourceGroupType.OutEnvironment)
            {
                shouldBypassSteamAudio = true;
            }
        }

        if (!shouldBypassSteamAudio)
        {
            shouldBypassSteamAudio = IsMixerBypassed(betterSource.source1.outputAudioMixerGroup);
        }

        if (forceStereo)
        {
            shouldBypassSteamAudio = true;
        }

        ProcessAudioSource(betterSource.source1, shouldBypassSteamAudio);

        var shouldEnableReflections = false;
        // if (betterSource.source1.outputAudioMixerGroup.name is "ClientPlayerMovement" or "Gunshots")
        // {
        //     shouldEnableReflections = true;
        //     EnableReflections(betterSource.source1);
        // }

        if (betterSource is ReverbSimpleSource reverbSimpleSource)
        {
            AudioSource reverb = ReverbSimpleSourceFieldRef(reverbSimpleSource);
            if (reverb != null) ProcessAudioSource(reverb, shouldBypassSteamAudio);
            if (shouldEnableReflections) EnableReflections(reverb);
        }
        else if (betterSource is SuperSource superSource)
        {
            if (superSource.source2 != null)
                ProcessAudioSource(superSource.source2, shouldBypassSteamAudio);
            if (shouldEnableReflections) EnableReflections(superSource.source2);

            if (superSource is ReverbSuperSource reverbSuperSource)
            {
                AudioSource a = ReverbSuperSourceAFieldRef(reverbSuperSource);
                AudioSource b = ReverbSuperSourceBFieldRef(reverbSuperSource);

                if (a != null) ProcessAudioSource(a, shouldBypassSteamAudio);
                if (b != null) ProcessAudioSource(b, shouldBypassSteamAudio);

                if (shouldEnableReflections) EnableReflections(a);
                if (shouldEnableReflections) EnableReflections(b);
            }
        }
    }

    private static void EnableReflections(AudioSource src)
    {
        // cache[src].steam.reflections = true;
        // cache[src].steam.reflectionsMixLevel = 10;
        // cache[src].steam.reflectionsType = ReflectionsType.Realtime;
        // cache[src].steam.directMixLevel = 1f;
    }

    private static void ProcessAudioSource(AudioSource src, bool shouldBypassSteamAudio)
    {
        var cache = GetOrAdd(src);
        bool wasBypassed = cache.bridge.isBypass;

        // we toggle occlusion back and forth to save on constant unnecessary raycasts
        if (shouldBypassSteamAudio)
        {
            if (!wasBypassed)
            {
                cache.steam.occlusion = false;

                cache.bridge.isBypass = true;
                src.spatialize = cache.bridge.spatialize;
                src.spatialBlend = cache.bridge.spatialBlend;
            }
        }
        else
        {
            if (wasBypassed)
            {
                cache.steam.occlusion = true;

                bool currentNativeSpatialize = src.spatialize;
                float currentNativeBlend = src.spatialBlend;

                cache.bridge.isBypass = false;

                src.spatialize = currentNativeSpatialize;
                src.spatialBlend = currentNativeBlend;
            }
        }
    }

    public static void Dispose()
    {
        foreach (var kvp in cache)
        {
            AudioSource src = kvp.Key;
            SteamSourceData data = kvp.Value;

            if (src != null)
            {
                if (data.bridge != null)
                {
                    data.bridge.isBypass = true; // bypass audiosource patches
                    src.spatialize = data.bridge.spatialize;
                    src.spatialBlend = data.bridge.spatialBlend;
                    Object.Destroy(data.bridge as MonoBehaviour);
                }

                if (data.steam != null)
                {
                    Object.Destroy(data.steam);
                }
            }
        }

        cache.Clear();
        MixerBypassCache.Clear();
    }
}