using EFT.Ballistics;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_ArmorPlateCollider_RicochetChance : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(BallisticCollider), nameof(BallisticCollider.RicochetChance));

    [PatchPrefix]
    static bool Prefix(ref float __result)
    {
        __result = 0f;
        return false;
    }
}

internal class Patch_BodyPartCollider_RicochetChance : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(BodyPartCollider), nameof(BodyPartCollider.RicochetChance));

    [PatchPrefix]
    static bool Prefix(ref float __result)
    {
        __result = 0f;
        return false;
    }
}