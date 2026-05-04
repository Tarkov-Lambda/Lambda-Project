using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;

// Whenever we spawn a fake ragdoll this method throws an error so we just skip it idk
public class Patch_method_10 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Weapon), nameof(Weapon.method_10));

    [PatchPrefix]
    static bool Prefix(Weapon __instance)
    {
        if (!H.IsInRaid()) return true;

        if (__instance.Buff == null)
        {
            return false;
        }
        return true;
    }
}