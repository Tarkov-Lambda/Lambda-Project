using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
{
    internal class Patch_QuickSlotView_ShowInfoPanel : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuickSlotView), nameof(QuickSlotView.ShowInfoPanel));
        }

        [PatchPostfix]
        private static void PatchPostfix(QuickSlotView __instance, Item item, TMP_Text ___Caption)
        {
            __instance.gameObject.SetActive(item != null);

            ___Caption.horizontalAlignment = TMPro.HorizontalAlignmentOptions.Left;
            ___Caption.margin = new Vector4(6.5f, 0, 0, 0);
        }
    }
}
