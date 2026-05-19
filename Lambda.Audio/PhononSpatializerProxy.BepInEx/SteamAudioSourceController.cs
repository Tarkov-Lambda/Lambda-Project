using System.Collections.Generic;
using SteamAudio;
using UnityEngine;

namespace PhononSpatializerProxy.BepInEx;

public struct SteamSourceData
{
    public SteamAudioSource steam;
    public IProxiedAudioSource bridge;
}

public static class SteamAudioSourceController
{
    public static readonly Dictionary<AudioSource, SteamSourceData> cache = new();

    public static SteamSourceData GetOrAdd(AudioSource audioSource)
    {
        if (cache.TryGetValue(audioSource, out var data))
            return data;

        // Save initial Unity spatial properties before we enable AudioSource patches for:
        // get/set_spatialize
        // get/set_spatialBlend
        // to completely proxy the original fields and give control to IProxiedAudioSource (unless we decide otherwise below)
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

        steamAudio.transmissionHigh = 0.25f;
        steamAudio.transmissionMid = 0.3f;
        steamAudio.transmissionLow = 0.4f;

        audioSource.spatialize = initialSpatialize;
        audioSource.spatialBlend = initialBlend;

        return data;
    }

    public static void ProcessAudioSource(AudioSource src, bool shouldBypassSteamAudio)
    {
        var cache = GetOrAdd(src);
        bool wasBypassed = cache.bridge.isBypass;

        // we toggle occlusion back and forth to save on constant unnecessary raycasts (in retrospect idk if this matters the way bettersource is designed)
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
    }
}