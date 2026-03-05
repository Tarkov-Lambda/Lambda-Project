using EFT.InventoryLogic;
using HarmonyLib;
using ifp.arena.bep.Core;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov
{
    // Whenever we spawn a fake ragdoll this method throws an error so we just skip it idk
    public class Patch_method_10 : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EFT.InventoryLogic.Weapon), nameof(EFT.InventoryLogic.Weapon.method_10));
        }

        [PatchPrefix]
        static bool Prefix(Weapon __instance)
        {
            if (!H.isInRaid()) return true;

            if (__instance.Buff == null)
            {
                return false;
            }
            return true;
        }
    }
}
