using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov.UI.WeaponBuilds
{
    internal class Patch_EditBuildScreen_Show : ModulePatch
    {
        public static event Action OnPostfix;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EditBuildScreen), nameof(EditBuildScreen.Show), 
                [typeof(Item), typeof(Item), typeof(InventoryController), typeof(ISession)]);
        }

        [PatchPostfix]
        private static void PatchPostfix(EditBuildScreen __instance)
        {
            try
            {
                OnPostfix?.Invoke();
            }
            catch { }
        }
    }
}
