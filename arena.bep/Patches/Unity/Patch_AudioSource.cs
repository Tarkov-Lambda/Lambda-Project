using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches;


// currently unused
internal class Patch_AudioSource_set_volume : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.volume));

    [PatchPrefix]
    public static bool Prefix(AudioSource __instance, ref float value)
    {
        if (SteamSourceDict.cache.TryGetValue(__instance, out var cache))
        {
            if (!cache.bridge.IsBypass)
            {
                value = 1f; // Force Unity volume to 1 if Steam Audio is handling attenuation
            }
        }
        return true;
    }
}

internal class Patch_AudioSource_set_spatialBlend : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.spatialBlend));

    [PatchPrefix]
    public static bool Prefix(AudioSource __instance, ref float value)
    {
        if (!SteamSourceDict.cache.TryGetValue(__instance, out var cache)) return true;
        if (cache.bridge.IsBypass) return true; // Let Unity handle it natively

        cache.bridge.spatialBlend = Mathf.Clamp01(value);
        value = 0f; // Force Unity native to 2D
        return true;
    }
}

internal class Patch_AudioSource_set_spatialize : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.spatialize));

    [PatchPrefix]
    public static bool Prefix(AudioSource __instance, ref bool value)
    {
        if (!SteamSourceDict.cache.TryGetValue(__instance, out var cache)) return true;
        if (cache.bridge.IsBypass) return true; // Let Unity handle it natively

        cache.bridge.spatialize = value;
        value = false; // Force Unity native to 2D
        return true;
    }
}

internal class Patch_AudioSource_get_spatialBlend : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(AudioSource), nameof(AudioSource.spatialBlend));

    [PatchPrefix]
    public static bool Prefix(AudioSource __instance, ref float __result)
    {
        if (!SteamSourceDict.cache.TryGetValue(__instance, out var cache)) return true;
        if (cache.bridge.IsBypass) return true; // Let Unity handle it natively

        __result = cache.bridge.spatialBlend;
        return false;
    }
}

internal class Patch_AudioSource_get_spatialize : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(AudioSource), nameof(AudioSource.spatialize));

    [PatchPrefix]
    public static bool Prefix(AudioSource __instance, ref bool __result)
    {
        if (!SteamSourceDict.cache.TryGetValue(__instance, out var cache)) return true;
        if (cache.bridge.IsBypass) return true; // Let Unity handle it natively

        __result = cache.bridge.spatialize;
        return false;
    }
}