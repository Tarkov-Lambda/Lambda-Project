using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Audio.SteamIntegration.Patches;

internal class Patch_BetterSource_SetLowPassFilterParameters : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.SetLowPassFilterParameters));

    [PatchPrefix]
    static bool Prefix() => false;
}

internal class Patch_BetterSource_SetHighPassFilterParameters : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.SetHighPassFilterParameters));

    [PatchPrefix]
    static bool Prefix() => false;
}

internal class Patch_BetterSource_IncludeInOcclusionProcess : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.IncludeInOcclusionProcess));

    [PatchPrefix]
    static bool Prefix(ref bool ___IncludedInOcclusionProcess)
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
        __instance.OcclusionRolloffScale = 1f;
        return false;
    }
}

public class Patch_BetterSource_Init : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.Init));

    [PatchPostfix]
    static void Postfix(BetterSource __instance)
    {
        // BetterSourceProxyRouter.LobotomizeBetterSource(__instance);
    }
}
