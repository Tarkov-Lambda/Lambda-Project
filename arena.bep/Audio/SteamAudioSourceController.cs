using System.Collections.Generic;
using Audio.ReverbSubsystem;
using EFT;
using HarmonyLib;
using ifp.arena.shared;
using SteamAudio;
using UnityEngine;
using UnityEngine.Audio;

public struct SteamSourceData
{
    public SteamAudioSource steam;
    public PhononDSPBridge bridge;
}

public static class SteamAudioSourceController
{
    public static AccessTools.FieldRef<BetterSource, AudioGroupPreset> PresetRef =
        AccessTools.FieldRefAccess<BetterSource, AudioGroupPreset>("Preset");

    public static AccessTools.FieldRef<BetterSource, bool> ForceStereoRef =
        AccessTools.FieldRefAccess<BetterSource, bool>("_forceStereo");

    public static AccessTools.FieldRef<ReverbSimpleSource, AudioSource> ReverbSimpleSourceFieldRef =
        AccessTools.FieldRefAccess<ReverbSimpleSource, AudioSource>("_reverbSource");

    public static AccessTools.FieldRef<ReverbSuperSource, AudioSource> ReverbSuperSourceAFieldRef =
        AccessTools.FieldRefAccess<ReverbSuperSource, AudioSource>("_reverbSourceA");

    public static AccessTools.FieldRef<ReverbSuperSource, AudioSource> ReverbSuperSourceBFieldRef =
        AccessTools.FieldRefAccess<ReverbSuperSource, AudioSource>("_reverbSourceB");

    private static readonly Dictionary<AudioMixerGroup, bool> MixerBypassCache = new();

    public static readonly Dictionary<AudioSource, SteamSourceData> cache = new();

    public static SteamSourceData GetOrAdd(AudioSource audioSource)
    {
        if (cache.TryGetValue(audioSource, out var data))
            return data;

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
        steamAudio.distanceAttenuationInput = DistanceAttenuationInput.CurveDriven;
        steamAudio.distanceAttenuationValue = 1f;
        steamAudio.airAbsorption = false;
        steamAudio.airAbsorptionInput = AirAbsorptionInput.SimulationDefined;
        steamAudio.occlusionType = OcclusionType.Raycast;
        steamAudio.occlusionRadius = 1.4f;
        steamAudio.occlusionSamples = 8;
        steamAudio.transmissionType = TransmissionType.FrequencyDependent;
        steamAudio.transmissionInput = TransmissionInput.UserDefined;
        // steamAudio.transmissionHigh = 0.2f;
        // steamAudio.transmissionMid = 0.4f;
        // steamAudio.transmissionLow = 0.5f;
        steamAudio.transmissionHigh = 0.5f;
        steamAudio.transmissionMid = 0.65f;
        steamAudio.transmissionLow = 0.75f;
        steamAudio.reflections = false;

        audioSource.spatialize = initialSpatialize;
        audioSource.spatialBlend = initialBlend;

        return data;
    }

    private static bool IsMixerBypassed(AudioMixerGroup mixer)
    {
        if (mixer == null) return false;

        if (MixerBypassCache.TryGetValue(mixer, out bool bypassed))
            return bypassed;

        string mixerName = mixer.name;
        bypassed = mixerName.Contains("ClientPlayer") ||
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
            if (type == BetterAudio.AudioSourceGroupType.Nonspatial ||
                type == BetterAudio.AudioSourceGroupType.NonspatialBypass ||
                type == BetterAudio.AudioSourceGroupType.Environment ||
                type == BetterAudio.AudioSourceGroupType.OutEnvironment)
            {
                shouldBypassSteamAudio = true;
            }
        }

        if (!shouldBypassSteamAudio)
        {
            shouldBypassSteamAudio = IsMixerBypassed(betterSource.source1.outputAudioMixerGroup);
        }

        // D.Log(betterSource.source1.outputAudioMixerGroup.name);
        // D.Log(betterSource.source1.spatialize.ToString());
        // D.Log(betterSource.source1.spatialBlend.ToString());
        // D.Log(shouldBypassSteamAudio.ToString());

        ProcessAudioSource(betterSource.source1, shouldBypassSteamAudio);

        if (betterSource is ReverbSimpleSource reverbSimpleSource)
        {
            AudioSource reverb = ReverbSimpleSourceFieldRef(reverbSimpleSource);
            if (reverb != null) ProcessAudioSource(reverb, shouldBypassSteamAudio);
        }
        else if (betterSource is SuperSource superSource)
        {
            if (superSource.source2 != null)
                ProcessAudioSource(superSource.source2, shouldBypassSteamAudio);

            if (superSource is ReverbSuperSource reverbSuperSource)
            {
                AudioSource a = ReverbSuperSourceAFieldRef(reverbSuperSource);
                AudioSource b = ReverbSuperSourceBFieldRef(reverbSuperSource);

                if (a != null) ProcessAudioSource(a, shouldBypassSteamAudio);
                if (b != null) ProcessAudioSource(b, shouldBypassSteamAudio);
            }
        }
    }

    private static void ProcessAudioSource(AudioSource src, bool shouldBypassSteamAudio)
    {
        var cache = GetOrAdd(src);
        bool wasBypassed = cache.bridge.IsBypass;

        if (shouldBypassSteamAudio)
        {
            if (!wasBypassed)
            {
                cache.bridge.IsBypass = true;
                src.spatialize = cache.bridge.spatialize;
                src.spatialBlend = cache.bridge.spatialBlend;
            }
        }
        else
        {
            if (wasBypassed)
            {
                bool currentNativeSpatialize = src.spatialize;
                float currentNativeBlend = src.spatialBlend;

                cache.bridge.IsBypass = false;

                src.spatialize = currentNativeSpatialize;
                src.spatialBlend = currentNativeBlend;
            }
        }
    }
}