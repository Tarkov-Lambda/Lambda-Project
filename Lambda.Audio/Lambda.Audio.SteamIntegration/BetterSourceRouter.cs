
using System.Collections.Generic;
using Audio.ReverbSubsystem;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

public class BetterSourceProxyRouter : Singleton<BetterSourceProxyRouter>
{
    public readonly static AccessTools.FieldRef<BetterSource, AudioGroupPreset> PresetRef = AccessTools.FieldRefAccess<BetterSource, AudioGroupPreset>("Preset");
    public readonly static AccessTools.FieldRef<BetterSource, bool> ForceStereoRef = AccessTools.FieldRefAccess<BetterSource, bool>("_forceStereo");
    public readonly static AccessTools.FieldRef<ReverbSimpleSource, AudioSource> ReverbSimpleSourceFieldRef = AccessTools.FieldRefAccess<ReverbSimpleSource, AudioSource>("_reverbSource");
    public readonly static AccessTools.FieldRef<ReverbSuperSource, AudioSource> ReverbSuperSourceAFieldRef = AccessTools.FieldRefAccess<ReverbSuperSource, AudioSource>("_reverbSourceA");
    public readonly static AccessTools.FieldRef<ReverbSuperSource, AudioSource> ReverbSuperSourceBFieldRef = AccessTools.FieldRefAccess<ReverbSuperSource, AudioSource>("_reverbSourceB");

    // Selection of mixers we do not route through steam audio
    private readonly static Dictionary<AudioMixerGroup, bool> MixerBypassCache = new();
    private readonly static HashSet<BetterSource> playingBetterSources = new();


    // it works idgaf
    // private static bool IsMixerBypassed(AudioMixerGroup mixer)
    // {
    //     if (mixer == null) return false;

    //     if (MixerBypassCache.TryGetValue(mixer, out bool bypassed))
    //         return bypassed;

    //     string mixerName = mixer.name;
    //     bypassed = mixerName.Contains("Ambient") ||
    //                mixerName.Contains("UI") ||
    //                mixerName.Contains("Music");

    //     MixerBypassCache[mixer] = bypassed;
    //     return bypassed;
    // }

    // public static void Process(BetterSource betterSource, bool? forceStereoOverride = null)
    // {
    //     SteamSourceData data = SteamAudioSourceController.GetOrAdd(betterSource.source1);

    //     // data.steam.distanceAttenuation = false;
    //     // data.steam.occlusion = false;
    //     // betterSource.source1.spatialize = false;
    //     // betterSource.source1.spatialBlend = 0f;

    //     bool forceStereo = forceStereoOverride ?? ForceStereoRef(betterSource);

    //     if (forceStereo)
    //     {
    //         data.steam.distanceAttenuation = false;
    //         data.steam.occlusion = false;
    //         betterSource.source1.spatialize = false;
    //         betterSource.source1.spatialBlend = 0f;

    //         if (betterSource is ReverbSimpleSource reverbSimpleSource)
    //         {
    //             SteamSourceData reverbSimpleSourceData = SteamAudioSourceController.GetOrAdd(betterSource.source1);
    //             reverbSimpleSourceData.steam.distanceAttenuation = false;
    //             reverbSimpleSourceData.steam.occlusion = false;
    //             ReverbSimpleSourceFieldRef(reverbSimpleSource).spatialize = false;
    //             ReverbSimpleSourceFieldRef(reverbSimpleSource).spatialBlend = 0f;
    //         }
    //     }
    //     else
    //     {
    //         data.steam.distanceAttenuation = true;
    //         data.steam.occlusion = true;
    //         betterSource.source1.spatialize = true;
    //         betterSource.source1.spatialBlend = 1f;

    //         if (betterSource is ReverbSimpleSource reverbSimpleSource)
    //         {
    //             SteamSourceData reverbSimpleSourceData = SteamAudioSourceController.GetOrAdd(betterSource.source1);
    //             reverbSimpleSourceData.steam.distanceAttenuation = true;
    //             reverbSimpleSourceData.steam.occlusion = true;
    //             ReverbSimpleSourceFieldRef(reverbSimpleSource).spatialize = true;
    //             ReverbSimpleSourceFieldRef(reverbSimpleSource).spatialBlend = 1f;
    //         }
    //     }

    // }

    // public static void LobotomizeBetterSource(BetterSource betterSource)
    // {
    //     betterSource.source1.enabled = true;
    //     SteamSourceData data = SteamAudioSourceController.ProcessAudioSource(betterSource.source1);

    //     if (betterSource is ReverbSimpleSource reverbSimpleSource)
    //     {
    //         ReverbSimpleSourceFieldRef(reverbSimpleSource).enabled = true;
    //         SteamAudioSourceController.ProcessAudioSource(ReverbSimpleSourceFieldRef(reverbSimpleSource));

    //     }
    //     else if (betterSource is SuperSource superSource)
    //     {
    //         superSource.source2.enabled = false;

    //         if (superSource is ReverbSuperSource reverbSuperSource)
    //         {
    //             ReverbSuperSourceAFieldRef(reverbSuperSource).enabled = false;
    //             ReverbSuperSourceBFieldRef(reverbSuperSource).enabled = false;
    //         }
    //     }

    // }
}