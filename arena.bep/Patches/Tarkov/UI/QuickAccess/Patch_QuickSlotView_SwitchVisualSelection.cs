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
            ___Caption.color = selected ? Color.black : new Color(0, 0, 0, 0f);
        }
    }
}
