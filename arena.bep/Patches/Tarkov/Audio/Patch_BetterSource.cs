using Audio.ReverbSubsystem;
using Audio.SpatialSystem;
using EFT;
using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches;

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

public class Patch_BetterSource_CheckBinauralAllowed : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.CheckBinauralAllowed));

    [PatchPrefix]
    static bool Prefix(BetterSource __instance)
    {
        bool forceStereo = SteamAudioSourceController.ForceStereoRef(__instance);

        if (forceStereo)
        {
            // If it's the local player disable spatialization so it plays 2D
            if (__instance.EnableSpatialization)
            {
                __instance.RefreshSpatialization(false);
            }
        }
        else
        {
            // bypass eft shit that turns off 3D audio when < 1.5m away
            var preset = SteamAudioSourceController.PresetRef(__instance);
            bool shouldSpatialize = preset != null && preset.SteamSpatialize && preset.DirectBinaural;
            __instance.RefreshSpatialization(shouldSpatialize);
        }

        return false;
    }
}

public class Patch_SimpleSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SimpleSource), nameof(SimpleSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, bool forceStereo = false)
    {
        SteamAudioSourceController.RouteBetterSource(__instance, forceStereo);
    }
}

public class Patch_ReverbSimpleSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSimpleSource), nameof(ReverbSimpleSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, bool forceStereo = false)
    {
        SteamAudioSourceController.RouteBetterSource(__instance, forceStereo);
    }
}

public class Patch_SuperSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SuperSource), nameof(SuperSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, bool forceStereo = false)
    {
        SteamAudioSourceController.RouteBetterSource(__instance, forceStereo);
    }
}

public class Patch_ReverbSuperSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSuperSource), nameof(ReverbSuperSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, bool forceStereo = false)
    {
        SteamAudioSourceController.RouteBetterSource(__instance, forceStereo);
    }
}

public class Patch_BetterSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, bool forceStereo = false)
    {
        SteamAudioSourceController.RouteBetterSource(__instance, forceStereo);
    }
}

public class Patch_BetterSource_PlayScheduled : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.PlayScheduled));

    [PatchPrefix]
    static void Prefix(BetterSource __instance)
    {
        SteamAudioSourceController.RouteBetterSource(__instance);
    }
}

public class Patch_SimpleSource_PlayScheduled : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SimpleSource), nameof(SimpleSource.PlayScheduled));

    [PatchPrefix]
    static void Prefix(SimpleSource __instance)
    {
        SteamAudioSourceController.RouteBetterSource(__instance);
    }
}

public class Patch_ReverbSuperSource_PlayScheduled : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSuperSource), nameof(ReverbSuperSource.PlayScheduled));

    [PatchPrefix]
    static void Prefix(ReverbSuperSource __instance)
    {
        SteamAudioSourceController.RouteBetterSource(__instance);
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
            unityFilter.cutoffFrequency = 22000f;
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