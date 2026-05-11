using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess.MagazineSelector;

internal class PatchGroup_AmmoSelector_NewLook : PatchGroup
{
    public static bool IsCreatingAmmoSelectorItems = false;

    public static bool IsAmmoSelectorContext(Component component)
    {
        if (IsCreatingAmmoSelectorItems) return true;
        return component != null && component.GetComponentInParent<AmmoSelector>() != null;
    }

    private static bool IsQuickAccessContext(Component component)
    {
        return component != null && component.GetComponentInParent<InventoryScreenQuickAccessPanel>() != null;
    }

    private class Patch_AmmoSelector_ShowAcceptableMags : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(AmmoSelector), nameof(AmmoSelector.ShowAcceptableMags));

        [PatchPrefix]
        static void Prefix() => IsCreatingAmmoSelectorItems = true;

        [PatchPostfix]
        private static void PatchPostfix(AmmoSelector __instance,
        ref Image ____cancelBackground,
        ref Color ____normalCancelColor,
        ref Color ____selectedCancelColor
        )
        {
            var cross = __instance.transform.Find("Cancel/Image");
            if (cross != null && cross.TryGetComponent<Image>(out var crossImage))
            {
                ____cancelBackground.enabled = false;
                ____cancelBackground = crossImage;
                ____cancelBackground.enabled = true;

                ____normalCancelColor = new Color(1, 1, 1, 0.5f);
                ____selectedCancelColor = new Color(1, 1, 1, 1.0f);

                var border = __instance.transform.Find("Cancel/Border");
                if (border != null) border.gameObject.SetActive(false);
            }
        }

        [PatchFinalizer]
        static void Finalizer() => IsCreatingAmmoSelectorItems = false;
    }

    private class Patch_GridItemView_UpdateInfoVisibility : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GridItemView), nameof(GridItemView.UpdateInfoVisibility));

        [PatchPostfix]
        static void PatchPostfix(GridItemView __instance, Image ___ColorPanel, Image ____border)
        {
            if (IsAmmoSelectorContext(__instance))
            {
                if (___ColorPanel != null) ___ColorPanel.enabled = false;
                if (____border != null) ____border.enabled = false;

                Transform infoPanel = __instance.transform.Find("Info Panel");
                if (infoPanel != null)
                {
                    Transform caption = infoPanel.Find("Caption");
                    if (caption != null) caption.gameObject.SetActive(false);

                    Transform bottomLeft = infoPanel.Find("BottomLayoutGroup/BottomLeftLayoutGroup");
                    if (bottomLeft != null) bottomLeft.gameObject.SetActive(false);

                    Transform bulletStats = infoPanel.Find("BottomLayoutGroup/BottomRightLayoutGroup/");
                    if (bottomLeft != null) bottomLeft.gameObject.SetActive(false);
                }
            }
            else if (!IsQuickAccessContext(__instance))
            {
                if (___ColorPanel != null) ___ColorPanel.enabled = true;
                if (____border != null) ____border.enabled = true;
            }
        }
    }

    private class Patch_AmmoSelector_RemoveItem : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(AmmoSelector), nameof(AmmoSelector.method_6));

        [PatchPrefix]
        static void Prefix(AmmoSelector __instance, int index)
        {
            var listField = AccessTools.Field(typeof(AmmoSelector), "list_1");
            var list = (List<GridItemView>)listField.GetValue(__instance);

            if (index < 0 || index >= list.Count)
                return;

            GridItemView view = list[index];
            if (view == null)
                return;

            RestoreGridItemView(view);
        }

        static void RestoreGridItemView(GridItemView view)
        {
            Image colorPanel = AccessTools.Field(typeof(GridItemView), "ColorPanel")?.GetValue(view) as Image;

            Image border = AccessTools.Field(typeof(GridItemView), "_border")?.GetValue(view) as Image;

            if (colorPanel != null)
                colorPanel.enabled = true;

            if (border != null)
                border.enabled = true;

            Transform infoPanel = view.transform.Find("Info Panel");
            if (infoPanel != null)
            {
                Transform caption = infoPanel.Find("Caption");
                if (caption != null) caption.gameObject.SetActive(true);

                Transform bottomLeft = infoPanel.Find("BottomLayoutGroup/BottomLeftLayoutGroup");
                if (bottomLeft != null) bottomLeft.gameObject.SetActive(true);

                // Transform Boolets = infoPanel.Find("BottomLayoutGroup/BottomRightLayoutGroup/Value");
                // if (Boolets != null)
                // {
                //     Boolets.GetComponent<TextMeshProUGUI>().fontMaterial.SetFloat("_UnderlayDilate", 0);
                //     Boolets.GetComponent<TextMeshProUGUI>().fontMaterial.SetFloat("_UnderlayWidth", 0);
                // }
            }

            Image mainImage = AccessTools.Field(typeof(ItemView), "MainImage")?.GetValue(view) as Image;

            if (mainImage != null)
            {
                mainImage.transform.localRotation = Quaternion.identity;
                mainImage.transform.localScale = Vector3.one;

                mainImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                mainImage.rectTransform.anchoredPosition = Vector2.zero;

                mainImage.material = null;
                mainImage.color = Color.white;
            }
        }
    }

    private class Patch_ItemView_UpdateColor : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ItemView), nameof(ItemView.UpdateColor));

        [PatchPostfix]
        static void PatchPostfix(ItemView __instance, bool ___bool_4, bool ___HighlightedGlobally, Image ___MainImage)
        {
            if (___MainImage == null) return;

            if (IsAmmoSelectorContext(__instance))
            {
                bool isHighlighted = ___bool_4 || ___HighlightedGlobally;
                ___MainImage.color = new Color(1, 1, 1, isHighlighted ? 1.0f : 0.4f);
            }
        }
    }

    private class Patch_ItemView_UpdateScale : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ItemView), nameof(ItemView.UpdateScale));

        [PatchPostfix]
        static void PatchPostfix(ItemView __instance, Image ___MainImage)
        {
            if (___MainImage == null) return;

            if (IsAmmoSelectorContext(__instance))
            {
                ___MainImage.transform.localRotation = Quaternion.identity;
                ___MainImage.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
                ___MainImage.rectTransform.pivot = new Vector2(0f, 0.5f);
                ___MainImage.rectTransform.anchoredPosition = new Vector2(-20f, 0f);

                if (PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial != null)
                {
                    ___MainImage.material = PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial;
                }
            }
            else if (!IsQuickAccessContext(__instance) && __instance.GetType() == typeof(GridItemView))
            {
                ___MainImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                ___MainImage.rectTransform.anchoredPosition = Vector2.zero;
                ___MainImage.material = null;
            }
        }
    }

    private class Patch_GridItemView_UpdateInfo : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GridItemView), nameof(GridItemView.UpdateInfo));

        [PatchPostfix]
        static void PatchPostfix(GridItemView __instance, Image ___MainImage)
        {
            if (___MainImage == null || !IsAmmoSelectorContext(__instance)) return;

            if (PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial != null)
            {
                ___MainImage.material = PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial;
            }

            ___MainImage.color = new Color(1, 1, 1, ___MainImage.color.a);
        }
    }

    internal class Patch_AmmoSelector_Position : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(AmmoSelector), nameof(AmmoSelector.ShowAcceptableMags));

        [PatchPostfix]
        private static void PatchPostfix(AmmoSelector __instance)
        {
            if (H.MainPlayer?.HandsController?.Item == null)
                return;

            if (!PatchGroup_QuickAccessPanel_ModifyItemIcon.ItemToImage.TryGetValue(
                H.MainPlayer.HandsController.Item,
                out Image weaponImage) || weaponImage == null)
                return;

            var weapRect = weaponImage.rectTransform;

            Vector3[] corners = new Vector3[4];
            weapRect.GetWorldCorners(corners);

            float weaponRight = corners[2].x;
            float weaponCenterY = (corners[1].y + corners[0].y) * 0.5f;

            var selectorRect = (RectTransform)__instance.transform;

            LayoutRebuilder.ForceRebuildLayoutImmediate(selectorRect);

            float selectorWorldHeight = selectorRect.rect.height * selectorRect.lossyScale.y;

            float gap = -18f;

            Vector3 newPos = new(
                weaponRight + gap,
                weaponCenterY + (selectorWorldHeight * 0.5f),
                __instance.transform.position.z
            );

            __instance.transform.position = newPos;
        }
    }
}