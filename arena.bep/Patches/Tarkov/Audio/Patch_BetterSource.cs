using Audio.ReverbSubsystem;
using Audio.SpatialSystem;
using EFT;
using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches;

// attach steam listener on player
internal class Patch_BetterSource_IncludeInOcclusionProcess : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.IncludeInOcclusionProcess));

    [PatchPrefix]
    static bool Prefix(BetterSource __instance, bool included, ref bool ___IncludedInOcclusionProcess)
    {
        ___IncludedInOcclusionProcess = false;
        return false;
    }
}

internal class Patch_BetterSource_ResetOcclusion : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.ResetOcclusion));

    [PatchPrefix]
    static bool Prefix(BetterSource __instance)
    {
        __instance.SetOcclusionVolumeFactor(1f);
        __instance.SetOcclusionRolloffScale(1f);
        __instance.ResetFilters();
        return false;
    }
}

public class Patch_SimpleSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SimpleSource), nameof(SimpleSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, AudioClip clip1, AudioClip clip2, float balance, float volume = 1f, bool forceStereo = false, bool oneShot = true)
    {
        SteamAudioSourceController.RouteAudioSource(__instance, clip1, forceStereo);
    }
}

public class Patch_ReverbSimpleSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSimpleSource), nameof(ReverbSimpleSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, AudioClip clip1, AudioClip clip2, float balance, float volume = 1f, bool forceStereo = false, bool oneShot = true)
    {
        SteamAudioSourceController.RouteAudioSource(__instance, clip1, forceStereo);
    }
}

public class Patch_SuperSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SuperSource), nameof(SuperSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, AudioClip clip1, AudioClip clip2, float balance, float volume = 1f, bool forceStereo = false, bool oneShot = true)
    {
        SteamAudioSourceController.RouteAudioSource(__instance, clip1, forceStereo);
    }
}



public class Patch_ReverbSuperSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSuperSource), nameof(ReverbSuperSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, AudioClip clip1, AudioClip clip2, float balance, float volume = 1f, bool forceStereo = false, bool oneShot = true)
    {
        SteamAudioSourceController.RouteAudioSource(__instance, clip1, forceStereo);
    }
}

public class Patch_BetterSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, AudioClip clip1, AudioClip clip2, float balance, float volume = 1f, bool forceStereo = false, bool oneShot = true)
    {
        SteamAudioSourceController.RouteAudioSource(__instance, clip1, forceStereo);
    }

}

public class Patch_BetterSource_SetOcclusionVolumeFactor : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.SetOcclusionVolumeFactor));

    [PatchPrefix]
    static bool Prefix(BetterSource __instance)
    {
        __instance.OcclusionVolumeFactor = 1f;
        return false;
    }
}

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
            unityFilter.cutoffFrequency = 22000f; // stinky
        }

        return false;
    }
}

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