using EFT;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov.UI.WeaponBuilds
{
    internal class Patch_OpenBuildWindow_Show : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(OpenBuildWindow), nameof(OpenBuildWindow.Show));
        }

        [PatchPostfix]
        private static void PatchPostfix(OpenBuildWindow __instance, 
            RagFairClass ragfair, 
            HandbookClass handbook, 
            WeaponBuildsStorageClass storage, 
            string? selectedWeaponTemplateId, 
            Action<WeaponBuildClass> onBuildSelected)
        {

        }
    }
}
