using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using Lambda.Core.Main.UI;
using SPT.Reflection.Patching;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Lambda.Core.Patches.Tarkov.UI.QuickAccess;

internal class PatchGroup_QuickAccessPanel_ModifyItemIcon : PatchGroup
{
    public static Material MatteMaterial { get; set; }

    public static ConditionalWeakTable<Item, Image> ItemToImage = new();

    private class Patch_QuickSlotItemView_UpdateInfo : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(QuickSlotItemView), nameof(QuickSlotItemView.UpdateInfo));

        [PatchPostfix]
        static void PatchPostfix(QuickSlotItemView __instance, Image ___MainImage) => ModifyItemIcon(__instance, ___MainImage);
    }

    private class Patch_QuickSlotItemView_UpdateScale : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(QuickSlotItemView), nameof(QuickSlotItemView.UpdateScale));

        [PatchPostfix]
        private static void PatchPostfix(QuickSlotItemView __instance, Image ___MainImage) => ModifyItemIcon(__instance, ___MainImage);
    }

    private class Patch_QuickSlotItemView_Create : ModulePatch
    {
        private static readonly FieldInfo Field_ItemView_MainImage = AccessTools.Field(typeof(ItemView), "MainImage");

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(QuickSlotItemView), nameof(QuickSlotItemView.Create));

        [PatchPostfix]
        private static void PatchPostfix(QuickSlotItemView __result) => ModifyItemIcon(__result, Field_ItemView_MainImage.GetValue(__result) as Image);
    }

    private static void ModifyItemIcon(QuickSlotItemView __instance, Image ___MainImage)
    {
        if (___MainImage == null) return;

        ___MainImage.transform.localRotation = Quaternion.identity;
        ___MainImage.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        ___MainImage.rectTransform.pivot = new Vector2(0f, 0.5f);
        ___MainImage.rectTransform.anchoredPosition = new Vector2(-20f, 0f);

        if (MatteMaterial != null)
        {
            ___MainImage.material = MatteMaterial;
            ___MainImage.color = new Color(1, 1, 1, 1);
        }

        ItemToImage.AddOrUpdate(__instance.Item, ___MainImage);
    }
}