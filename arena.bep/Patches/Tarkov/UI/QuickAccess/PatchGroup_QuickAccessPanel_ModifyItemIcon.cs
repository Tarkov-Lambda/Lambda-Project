using Comfort.Common;
using EFT;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using ifp.arena.bep.Core.UI;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess;

internal class PatchGroup_QuickAccessPanel_ModifyItemIcon : PatchGroup
{
    private class Patch_QuickSlotItemView_UpdateInfo : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
            => AccessTools.Method(typeof(QuickSlotItemView), nameof(QuickSlotItemView.UpdateInfo));

        [PatchPostfix]
        static void PatchPostfix(Image ___MainImage)
            => ModifyItemIcon(___MainImage);
    }

    private class Patch_QuickSlotItemView_UpdateScale : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
            => AccessTools.Method(typeof(QuickSlotItemView), nameof(QuickSlotItemView.UpdateScale));

        [PatchPostfix]
        private static void PatchPostfix(Image ___MainImage)
            => ModifyItemIcon(___MainImage);
    }

    private static void ModifyItemIcon(Image ___MainImage)
    {
        ___MainImage.transform.localRotation = Quaternion.identity;
        ___MainImage.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        ___MainImage.rectTransform.pivot = new Vector2(0f, 0.5f);
        ___MainImage.rectTransform.anchoredPosition = new Vector2(-20f, 0f);

        if (Singleton<UIManager>.Instantiated)
        {
            ___MainImage.material = Singleton<UIManager>.Instance.MatteMaterial;
            ___MainImage.color = new Color(1, 1, 1, 1);
        }
    }
}