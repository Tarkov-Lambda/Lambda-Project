using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;
using TMPro;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
{
    internal class PatchGroup_QuickAccessPanel_HideEmptySlots : PatchGroup
    {
        private class Patch_BoundItemView_Show : ModulePatch
        {
            protected override MethodBase GetTargetMethod() 
                => AccessTools.Method(typeof(BoundItemView), nameof(BoundItemView.Show));

            [PatchPostfix]
            private static void PatchPostfix(BoundItemView __instance, InventoryController inventoryController, ItemUiContext itemUiContext)
            {
                bool itemBinded = false;

                if (inventoryController.Inventory.FastAccess.BoundItems.TryGetValue(__instance.BoundIndex, out var item))
                {
                    itemBinded = item != null;
                }

                __instance.gameObject.SetActive(itemBinded);
            }
        }
        private class Patch_BoundSlotView_Show : ModulePatch
        {
            protected override MethodBase GetTargetMethod() 
                => AccessTools.Method(typeof(BoundSlotView), nameof(BoundSlotView.Show));

            [PatchPostfix]
            private static void PatchPostfix(BoundSlotView __instance, Slot slot)
            {
                __instance.gameObject.SetActive(slot.ContainedItem != null);
            }
        }
        private class Patch_FastAccessGrenadeItemView_SetNewTopPriorityGrenade : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
                => AccessTools.Method(typeof(FastAccessGrenadeItemView), nameof(FastAccessGrenadeItemView.method_2));

            [PatchPostfix]
            private static void PatchPostfix(FastAccessGrenadeItemView __instance, Item ___Item)
            {
                __instance.gameObject.SetActive(___Item != null);
            }
        }
        private class Patch_FastAccessGrenadeItemView_Show : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
                => AccessTools.Method(typeof(FastAccessGrenadeItemView), nameof(FastAccessGrenadeItemView.Show));

            [PatchPostfix]
            private static void PatchPostfix(FastAccessGrenadeItemView __instance, Item ___Item)
            {
                __instance.gameObject.SetActive(___Item != null);
            }
        }
        private class Patch_QuickSlotView_Show : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
                => AccessTools.Method(typeof(QuickSlotView), nameof(QuickSlotView.RemoveItemView));

            [PatchPostfix]
            private static void PatchPostfix(QuickSlotView __instance)
            {
                __instance.gameObject.SetActive(false);
            }
        }
        private class Patch_QuickSlotView_ShowInfoPanel : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
                => AccessTools.Method(typeof(QuickSlotView), nameof(QuickSlotView.ShowInfoPanel));

            [PatchPostfix]
            private static void PatchPostfix(QuickSlotView __instance, Item item, TMP_Text ___Caption)
            {
                __instance.gameObject.SetActive(item != null);
            }
        }
    }
}
