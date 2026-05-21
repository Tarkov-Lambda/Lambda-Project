using HarmonyLib;
using UnityEngine;

namespace PhononSpatializerProxy.BepInEx.Patches;

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.spatialBlend), MethodType.Setter)]
internal class Patch_AudioSource_set_spatialBlend
{
    [HarmonyPrefix]
    public static bool Prefix(AudioSource __instance, ref float value)
    {
        if (!SteamAudioSourceController.cache.TryGetValue(__instance, out var cache))
            return true;

        if (cache.bridge.isBypass)
            return true;  // Let Unity handle it natively

        cache.bridge.spatialBlend = Mathf.Clamp01(value);
        value = 0f; // unity spatialBlend must be forced to 2d to give us full audio output control
        return true;
    }
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.spatialize), MethodType.Setter)]
internal class Patch_AudioSource_set_spatialize
{
    [HarmonyPrefix]
    public static bool Prefix(AudioSource __instance, ref bool value)
    {
        if (!SteamAudioSourceController.cache.TryGetValue(__instance, out var cache))
            return true;

        if (cache.bridge.isBypass)
            return true;

        cache.bridge.spatialize = value;
        value = false;
        return true;
    }
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.spatialBlend), MethodType.Getter)]
internal class Patch_AudioSource_get_spatialBlend
{
    [HarmonyPrefix]
    public static bool Prefix(AudioSource __instance, ref float __result)
    {
        if (!SteamAudioSourceController.cache.TryGetValue(__instance, out var cache))
            return true;

        if (cache.bridge.isBypass)
            return true;

        __result = cache.bridge.spatialBlend;
        return false;
    }
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.spatialize), MethodType.Getter)]
internal class Patch_AudioSource_get_spatialize
{
    [HarmonyPrefix]
    public static bool Prefix(AudioSource __instance, ref bool __result)
    {
        if (!SteamAudioSourceController.cache.TryGetValue(__instance, out var cache))
            return true;

        if (cache.bridge.isBypass)
            return true;

        __result = cache.bridge.spatialize;
        return false;
    }
}