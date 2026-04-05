using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches;

internal class Patch_AudioSource_set_volume : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.volume));

    [PatchPrefix]
    public static bool Prefix(AudioSource __instance, ref float value)
    {
        if (SteamSourceDict.cache.ContainsKey(__instance))
        {
            value = 1f;
        }

        return true;
    }
}

// Proxying all spatial setter/getters to Steam Audio DSP Bridge
internal class Patch_AudioSource_set_spatialBlend : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.spatialBlend));

    [PatchPrefix]
    public static bool Prefix(AudioSource __instance, ref float value)
    {
        if (!SteamSourceDict.cache.ContainsKey(__instance)) return true;

        var spatCache = SteamSourceDict.cache[__instance];

        spatCache.bridge.spatialBlendOverride = Mathf.Clamp01(value);
        // D.Log("SETTING spatialBlend");

        __instance.spatialize = false;
        value = 0f;
        return true;
    }
}

internal class Patch_AudioSource_set_spatialize : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.spatialize));

    [PatchPrefix]
    public static bool Prefix(AudioSource __instance, ref bool value)
    {
        if (SteamSourceDict.cache.ContainsKey(__instance))
        {
            value = false;
        }

        return true;
    }
}

internal class Patch_AudioSource_get_spatialBlend : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(AudioSource), nameof(AudioSource.spatialBlend));

    [PatchPrefix]
    public static bool Prefix(AudioSource __instance, ref float __result)
    {
        if (!SteamSourceDict.cache.ContainsKey(__instance)) return true;

        var spatCache = SteamSourceDict.cache[__instance];

        __result = spatCache.bridge.spatialBlendOverride;
        // D.Log("Getting spatialBlend");

        return false;
    }
}