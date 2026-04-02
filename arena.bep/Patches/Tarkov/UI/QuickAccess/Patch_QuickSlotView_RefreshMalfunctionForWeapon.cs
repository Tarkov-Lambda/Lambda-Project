using EFT;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine.UI;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
{
    internal class Patch_QuickSlotView_RefreshMalfunctionForWeapon : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuickSlotView), nameof(QuickSlotView.method_0));
        }

        [PatchPostfix]
        private static void PatchPostfix(QuickSlotView __instance, Image ___InstallPlace)
        {
            ___InstallPlace.enabled = false;
        }
    }
}
