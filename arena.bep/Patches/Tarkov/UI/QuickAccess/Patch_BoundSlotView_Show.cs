using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
{
    internal class Patch_BoundSlotView_Show : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BoundSlotView), nameof(BoundSlotView.Show));
        }

        [PatchPostfix]
        private static void PatchPostfix(BoundSlotView __instance, Slot slot)
        {
            __instance.gameObject.SetActive(slot.ContainedItem != null);
        }
    }
}
