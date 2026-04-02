using EFT;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
{
    internal class Patch_QuickSlotView_SwitchVisualSelection : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuickSlotView), nameof(QuickSlotView.SwitchVisualSelection));
        }

        [PatchPostfix]
        private static void PatchPostfix(QuickSlotView __instance, bool selected, CustomTextMeshProUGUI ___Caption)
        {
            ___Caption.gameObject.SetActive(selected);
            ___Caption.color = Color.black;
            ___Caption.horizontalAlignment = TMPro.HorizontalAlignmentOptions.Left;
        }
    }
}
