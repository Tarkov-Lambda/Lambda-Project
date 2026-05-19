using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using Audio.SpatialSystem;

namespace Lambda.Audio.SteamIntegration.Patches;

public class Patch_SpatialLowPassFilter_CalculateFrequency : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SpatialLowPassFilter), nameof(SpatialLowPassFilter.CalculateFrequency));

    [PatchPrefix]
    static bool Prefix(SpatialLowPassFilter __instance)
    {
        var unityFilter = __instance.GetComponent<AudioLowPassFilter>();
        if (unityFilter != null)
        {
            unityFilter.enabled = false;
            unityFilter.cutoffFrequency = 22000f;
        }

        return false;
    }
}