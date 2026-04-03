using System.Collections.Generic;
using ifp.arena.bep.Patches;
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
    public static SteamSourceData GetOrAdd(AudioSource audioSource)
    {
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