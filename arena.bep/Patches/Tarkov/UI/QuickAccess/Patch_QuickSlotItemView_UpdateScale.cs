using EFT;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
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
            ___MainImage.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            ___MainImage.rectTransform.pivot = new Vector2(0f, 0.5f);
            ___MainImage.rectTransform.anchoredPosition = new Vector2(-20f, 0f);
        }
    }
}
