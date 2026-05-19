using System.Collections.Generic;
using Audio.ReverbSubsystem;
using EFT;
using HarmonyLib;
using PhononSpatializerProxy.BepInEx;
using UnityEngine;
using UnityEngine.Audio;

public static class BetterSourceProxyRouter
{
    public static AccessTools.FieldRef<BetterSource, AudioGroupPreset> PresetRef = AccessTools.FieldRefAccess<BetterSource, AudioGroupPreset>("Preset");
    public static AccessTools.FieldRef<BetterSource, bool> ForceStereoRef = AccessTools.FieldRefAccess<BetterSource, bool>("_forceStereo");
    public static AccessTools.FieldRef<ReverbSimpleSource, AudioSource> ReverbSimpleSourceFieldRef = AccessTools.FieldRefAccess<ReverbSimpleSource, AudioSource>("_reverbSource");
    public static AccessTools.FieldRef<ReverbSuperSource, AudioSource> ReverbSuperSourceAFieldRef = AccessTools.FieldRefAccess<ReverbSuperSource, AudioSource>("_reverbSourceA");
    public static AccessTools.FieldRef<ReverbSuperSource, AudioSource> ReverbSuperSourceBFieldRef = AccessTools.FieldRefAccess<ReverbSuperSource, AudioSource>("_reverbSourceB");

    // Selection of mixers we do not route through steam audio
    private static readonly Dictionary<AudioMixerGroup, bool> MixerBypassCache = new();

    // it works idgaf
    private static bool IsMixerBypassed(AudioMixerGroup mixer)
    {
        if (mixer == null) return false;

        if (MixerBypassCache.TryGetValue(mixer, out bool bypassed))
            return bypassed;

        string mixerName = mixer.name;
        bypassed = mixerName.Contains("Ambient") ||
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

        SteamAudioSourceController.ProcessAudioSource(betterSource.source1, shouldBypassSteamAudio);

        if (betterSource is ReverbSimpleSource reverbSimpleSource)
        {
            AudioSource reverb = ReverbSimpleSourceFieldRef(reverbSimpleSource);
            if (reverb != null) SteamAudioSourceController.ProcessAudioSource(reverb, shouldBypassSteamAudio);
        }
        else if (betterSource is SuperSource superSource)
        {
            if (superSource.source2 != null)
                SteamAudioSourceController.ProcessAudioSource(superSource.source2, shouldBypassSteamAudio);

            if (superSource is ReverbSuperSource reverbSuperSource)
            {
                AudioSource a = ReverbSuperSourceAFieldRef(reverbSuperSource);
                AudioSource b = ReverbSuperSourceBFieldRef(reverbSuperSource);

                if (a != null) SteamAudioSourceController.ProcessAudioSource(a, shouldBypassSteamAudio);
                if (b != null) SteamAudioSourceController.ProcessAudioSource(b, shouldBypassSteamAudio);
            }
        }
    }

    public static void Dispose()
    {
        MixerBypassCache.Clear();
    }
}