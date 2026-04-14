using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov.UI.WeaponBuilds
{
    internal class Patch_EditBuildScreen_UpdateItem : ModulePatch
    {
        public static event Action<Item> OnPostfix;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EditBuildScreen), nameof(EditBuildScreen.UpdateItem));
        }

        [PatchPostfix]
        private static void PatchPostfix(EditBuildScreen __instance, Item newItem)
        {
            OnPostfix?.Invoke(newItem);
        }
    }
}
