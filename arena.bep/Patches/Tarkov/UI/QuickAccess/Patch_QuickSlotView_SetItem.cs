using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
{
    internal class Patch_QuickSlotView_ShowInfoPanel : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuickSlotView), nameof(QuickSlotView.ShowInfoPanel));
        }

        [PatchPostfix]
        private static void PatchPostfix(QuickSlotView __instance, Item item)
        {
            __instance.gameObject.SetActive(item != null);
        }
    }
}
