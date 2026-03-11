using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

using ItemsTabController = EFT.UI.ItemsPanel.GClass3802;

namespace ifp.arena.bep.Patches.Tarkov.UI
{
    public class Patch_ItemsTabController_Show : ModulePatch
    {
        public static event Action<CompoundItem> OnShow;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemsTabController), nameof(ItemsTabController.Show));
        }

        [PatchPostfix]
        static void Postfix(ItemsTabController __instance)
        {
            OnShow?.Invoke(__instance.CompoundItem_0);
        }
    }
}
