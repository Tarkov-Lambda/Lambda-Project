using System;
using System.Runtime.CompilerServices;
using SteamAudio;
using UnityEngine;

namespace PhononSpatializerProxy
{
    public struct SteamSourceData
    {
        public SteamAudioSource steam;
        public PhononDSPBridge proxy;
    }

    public static class AudioSourceStateBypass
    {
        [ThreadStatic]
        public static bool Bypass;
    }

    public static class SteamAudioSourceController
    {
        static SteamAudioSourceController()
        {
            PhononDSPBridge.OnBridgeEnabled += OnBridgeEnabled;
            PhononDSPBridge.OnBridgeDisabled += OnBridgeDisabled;
        }

        public static readonly ConditionalWeakTable<AudioSource, StrongBox<SteamSourceData>> cache = new();

        public static void OnBridgeEnabled(PhononDSPBridge bridge)
        {

        }

        public static void OnBridgeDisabled(PhononDSPBridge bridge)
        {

        }

        public static SteamSourceData GetOrAdd(AudioSource audioSource)
        {
            if (cache.TryGetValue(audioSource, out var box))
            {
                box.Value.steam.distanceAttenuation = true;
                return box.Value;
            }
            var data = new SteamSourceData
            {
                steam = audioSource.gameObject.GetComponent<SteamAudioSource>(),
                proxy = audioSource.gameObject.GetComponent<PhononDSPBridge>()
            };

            RerouteSpatialValues(data, audioSource);

            cache.Add(audioSource, new StrongBox<SteamSourceData>(data));

            ApplyDefaultConfiguration(data.steam);

            return data;
        }

        public static void RerouteSpatialValues(SteamSourceData data, AudioSource audioSource)
        {
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
        }

        public static void RestoreSpatialValues(SteamSourceData data, AudioSource audioSource)
        {
            AudioSourceStateBypass.Bypass = true;
            try
            {
                audioSource.spatialBlend = data.proxy.spatialBlend;
                audioSource.spatialize = data.proxy.spatialize;
            }
            finally
            {
                AudioSourceStateBypass.Bypass = false;
            }
        }

        public static void ApplyDefaultConfiguration(SteamAudioSource steamAudio)
        {
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
        }
    }
}