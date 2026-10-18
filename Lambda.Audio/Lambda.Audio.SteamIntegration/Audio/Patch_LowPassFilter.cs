using System.Reflection;
using Audio.SpatialSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace Lambda.Audio.SteamIntegration.Patches;

internal class Patch_SpatialLowPassFilter_CalculateFrequency : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(
            typeof(SpatialLowPassFilter),
            nameof(SpatialLowPassFilter.CalculateFrequency)
        );

    [PatchPrefix]
    static bool Prefix(AudioLowPassFilter ____filter)
    {
        ____filter.enabled = false;
        ____filter.cutoffFrequency = 22000f;
        return false;
    }
}
