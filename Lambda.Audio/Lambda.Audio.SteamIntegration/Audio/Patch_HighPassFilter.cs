using System.Reflection;
using Audio.SpatialSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace Lambda.Audio.SteamIntegration.Patches;

internal class Patch_SpatialHighPassFilter_CalculateFrequency : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(
            typeof(SpatialHighPassFilter),
            nameof(SpatialHighPassFilter.CalculateFrequency)
        );

    [PatchPrefix]
    static bool Prefix(AudioHighPassFilter ____filter)
    {
        ____filter.enabled = false;
        ____filter.cutoffFrequency = 10f;
        return false;
    }
}
