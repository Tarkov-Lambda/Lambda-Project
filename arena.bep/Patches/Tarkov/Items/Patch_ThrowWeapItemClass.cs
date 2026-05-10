using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;


public class Patch_ThrowWeapItemClass_FragmentsCount : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(ThrowWeapItemClass), nameof(ThrowWeapItemClass.FragmentsCount));

    [PatchPrefix]
    static bool Prefix(ThrowWeapItemClass __instance, ref int __result)
    {
        if (Hardcode.MOLOTOV_GRENADE == __instance.TemplateId)
        {
            __result = 1;
            return false;
        }
        return true;
    }
}


public class Patch_ThrowWeapItemClass_MinFragmentDamage : ModulePatch
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

public class Patch_ThrowWeapItemClass_MaxFragmentDamage : ModulePatch
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