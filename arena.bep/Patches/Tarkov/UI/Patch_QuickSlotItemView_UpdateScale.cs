using EFT;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ifp.arena.bep.Patches.Tarkov.UI
{
    internal class Patch_QuickSlotItemView_UpdateScale : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuickSlotItemView), nameof(QuickSlotItemView.UpdateScale));
        }

        [PatchPostfix]
        private static void PatchPostfix(QuickSlotItemView __instance, Image ___MainImage)
        {
            ___MainImage.transform.localRotation = Quaternion.identity;
        }
    }
}
