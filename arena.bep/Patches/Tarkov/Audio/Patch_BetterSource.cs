using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using System.Reflection;

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