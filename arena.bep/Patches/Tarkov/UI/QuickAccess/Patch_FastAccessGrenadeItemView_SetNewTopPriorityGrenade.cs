using EFT;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
{
    internal class Patch_FastAccessGrenadeItemView_SetNewTopPriorityGrenade : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(FastAccessGrenadeItemView), nameof(FastAccessGrenadeItemView.method_2));
        }

        [PatchPostfix]
        private static void PatchPostfix(FastAccessGrenadeItemView __instance)
        {
            __instance.gameObject.SetActive(__instance.ThrowWeapItemClass != null);
        }
    }
}
