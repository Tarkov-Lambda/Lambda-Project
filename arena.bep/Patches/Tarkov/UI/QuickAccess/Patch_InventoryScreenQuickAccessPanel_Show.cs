using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using static EFT.UI.InventoryScreenQuickAccessPanel;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
{
    internal class Patch_InventoryScreenQuickAccessPanel_Show : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InventoryScreenQuickAccessPanel), nameof(InventoryScreenQuickAccessPanel.Show),
                [typeof(InventoryController), typeof(ItemUiContext), typeof(GamePlayerOwner), typeof(InsuranceCompanyClass)]);
        }

        [PatchPostfix]
        private static void PatchPostfix(InventoryScreenQuickAccessPanel __instance, BoundSlotViewDictionary ____boundSlots)
        {

        }
    }
}
