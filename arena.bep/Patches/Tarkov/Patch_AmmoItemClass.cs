using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;

public class Patch_AmmoItemClass_RicochetChance : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(AmmoItemClass), nameof(AmmoItemClass.RicochetChance));

    [PatchPrefix]
    static bool Prefix(ref float __result)
    {
        __result = 0f;
        return false;
    }
}