using System;
using System.Runtime.CompilerServices;
using SteamAudio;
using UnityEngine;

namespace PhononSpatializerProxy.BepInEx
{
    public struct SteamSourceData
    {
        public SteamAudioSource steam;
        public PhononDSPBridge proxy;
    }

    internal static class AudioSourceStateBypass
    {
        [ThreadStatic]
        public static bool Bypass;
    }

    public static class SteamAudioSourceController
    {
        public static readonly ConditionalWeakTable<AudioSource, StrongBox<SteamSourceData>> cache = new();

        public static SteamSourceData GetOrAdd(AudioSource audioSource)
        {
            if (cache.TryGetValue(audioSource, out var box))
            {
                box.Value.steam.distanceAttenuation = true;
                return box.Value;
            }
            var data = new SteamSourceData
            {
                steam = audioSource.gameObject.GetOrAddComponent<SteamAudioSource>(),
                proxy = audioSource.gameObject.GetOrAddComponent<PhononDSPBridge>()
            };

            AudioSourceStateBypass.Bypass = true;
            try
            {
                data.proxy.spatialBlend = audioSource.spatialBlend;
                data.proxy.spatialize = audioSource.spatialize;

                audioSource.spatialBlend = 0f;
                audioSource.spatialize = false;
            }
            finally
            {
                AudioSourceStateBypass.Bypass = false;
            }

            cache.Add(audioSource, new StrongBox<SteamSourceData>(data));

            SteamAudioSource steamAudio = data.steam;

            steamAudio.occlusion = true;
            steamAudio.transmission = true;

            steamAudio.distanceAttenuation = true;

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

            return data;
        }

        public static SteamSourceData ProcessAudioSource(AudioSource src)
        {
            return GetOrAdd(src);
        }
    }
}