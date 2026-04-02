using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
{
    internal class Patch_BoundItemView_Show : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BoundItemView), nameof(BoundItemView.Show));
        }

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
}
