using Audio.ReverbSubsystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Audio.SteamIntegration.Patches;

internal class Patch_BetterSource_SetLowPassFilterParameters : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.SetLowPassFilterParameters));

    [PatchPrefix]
    static bool Prefix()
    {
        // Prevent EFT from turning on the Unity AudioLowPassFilter
        return false;
    }
}

internal class Patch_BetterSource_SetHighPassFilterParameters : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.SetHighPassFilterParameters));

    [PatchPrefix]
    static bool Prefix()
    {
        // Prevent EFT from turning on the Unity AudioHighPassFilter
        return false;
    }
}

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
        bool forceStereo = BetterSourceProxyRouter.ForceStereoRef(__instance);

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
            var preset = BetterSourceProxyRouter.PresetRef(__instance);
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
        BetterSourceProxyRouter.RouteBetterSource(__instance, forceStereo);
    }
}

public class Patch_ReverbSimpleSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSimpleSource), nameof(ReverbSimpleSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, bool forceStereo = false)
    {
        BetterSourceProxyRouter.RouteBetterSource(__instance, forceStereo);
    }
}

public class Patch_SuperSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SuperSource), nameof(SuperSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, bool forceStereo = false)
    {
        BetterSourceProxyRouter.RouteBetterSource(__instance, forceStereo);
    }
}

public class Patch_ReverbSuperSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSuperSource), nameof(ReverbSuperSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, bool forceStereo = false)
    {
        BetterSourceProxyRouter.RouteBetterSource(__instance, forceStereo);
    }
}

public class Patch_BetterSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.Play));

    [PatchPrefix]
    static void Prefix(BetterSource __instance, bool forceStereo = false)
    {
        BetterSourceProxyRouter.RouteBetterSource(__instance, forceStereo);
    }
}

public class Patch_BetterSource_PlayScheduled : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.PlayScheduled));

    [PatchPrefix]
    static void Prefix(BetterSource __instance)
    {
        BetterSourceProxyRouter.RouteBetterSource(__instance);
    }
}

public class Patch_SimpleSource_PlayScheduled : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SimpleSource), nameof(SimpleSource.PlayScheduled));

    [PatchPrefix]
    static void Prefix(SimpleSource __instance)
    {
        BetterSourceProxyRouter.RouteBetterSource(__instance);
    }
}

public class Patch_ReverbSuperSource_PlayScheduled : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSuperSource), nameof(ReverbSuperSource.PlayScheduled));

    [PatchPrefix]
    static void Prefix(ReverbSuperSource __instance)
    {
        BetterSourceProxyRouter.RouteBetterSource(__instance);
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

internal class Patch_BetterSource_SetOcclusionRolloffScale : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.SetOcclusionRolloffScale));

    [PatchPrefix]
    static bool Prefix(BetterSource __instance)
    {
        // Force the scale to stay at 1.0, bypassing EFT's distance shrink and fixing the native pooling bug
        __instance.OcclusionRolloffScale = 1f;
        return false;
    }
}