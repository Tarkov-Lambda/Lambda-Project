using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using Audio.ReverbSubsystem;
using UnityEngine;
using EFT;
using Audio.SpatialSystem;

namespace Lambda.Core.Patches;

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