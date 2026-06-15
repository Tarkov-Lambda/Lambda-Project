using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;


internal class Patch_ThrowWeapItemClass_FragmentsCount : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(ThrowWeapItemClass), nameof(ThrowWeapItemClass.FragmentsCount));

    [PatchPrefix]
    static bool Prefix(ThrowWeapItemClass __instance, ref int __result)
    {
        if (Hardcode.MOLOTOV_GRENADE == __instance.TemplateId)
        {
            __result = 0;
            return false;
        }
        return true;
    }
}


internal class Patch_ThrowWeapItemClass_MinFragmentDamage : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(ThrowWeapItemClass), nameof(ThrowWeapItemClass.MinFragmentDamage));

    [PatchPrefix]
    static bool Prefix(ThrowWeapItemClass __instance, ref float __result)
    {
        if (Hardcode.MOLOTOV_GRENADE == __instance.TemplateId)
        {
            __result = 5f;
            return false;
        }
        return true;
    }
}

internal class Patch_ThrowWeapItemClass_MaxFragmentDamage : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(ThrowWeapItemClass), nameof(ThrowWeapItemClass.MaxFragmentDamage));

    [PatchPrefix]
    static bool Prefix(ThrowWeapItemClass __instance, ref float __result)
    {
        if (Hardcode.MOLOTOV_GRENADE == __instance.TemplateId)
        {
            __result = 10f;
            return false;
        }
        return true;
    }
}

internal class Patch_ThrowWeapItemClass_MinTimeToContactExplode : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(ThrowWeapItemClass), nameof(ThrowWeapItemClass.MinTimeToContactExplode));

    [PatchPrefix]
    static bool Prefix(ThrowWeapItemClass __instance, ref float __result)
    {
        if (Hardcode.MOLOTOV_GRENADE == __instance.TemplateId)
        {
            __result = 0.25f;
            return false;
        }
        return true;
    }
}