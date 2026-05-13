using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using Audio.SpatialSystem;

namespace Lambda.Core.Patches;

public class Patch_SpatialHighPassFilter_CalculateFrequency : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SpatialHighPassFilter), nameof(SpatialHighPassFilter.CalculateFrequency));

    [PatchPrefix]
    static bool Prefix(SpatialHighPassFilter __instance)
    {
        var unityFilter = __instance.GetComponent<AudioHighPassFilter>();
        if (unityFilter != null)
        {
            unityFilter.enabled = false;
            unityFilter.cutoffFrequency = 10f;
        }

        return false;
    }
}