using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess;

internal class PatchGroup_GrenadeSelector_NewLook : PatchGroup
{
    const float SELECTED_ALPHA = 1.0f;
    const float UNSELECTED_ALPHA = 0.5f;

    internal class Patch_GrenadeSelector_Awake : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GrenadeSelector), nameof(GrenadeSelector.Awake));
        }

        [PatchPostfix]
        private static void PatchPostfix(GrenadeSelector __instance,
            ref Vector2 ____arrowOffset,

            ref Image ____cancelBackground,
            ref Color ____normalCancelColor,
            ref Color ____selectedCancelColor
            )
        {
            ____arrowOffset = new Vector2(70, 50);


            var cross = __instance.transform.Find("Cancel/Image");
            if (cross != null && cross.TryGetComponent<Image>(out var crossImage))
            {
                ____cancelBackground.enabled = false;
                ____cancelBackground = crossImage;
                ____cancelBackground.enabled = true;

                ____normalCancelColor = new Color(1, 1, 1, 0.5f);
                ____selectedCancelColor = new Color(1, 1, 1, 1.0f);

                var border = __instance.transform.Find("Cancel/Border");
                if (border != null)
                    border.gameObject.SetActive(false);
            }
        }
    }

    class Patch_FastAccessGrenadeGridItemView_Create : ModulePatch
    {
        static readonly FieldInfo Field_ItemView__border = AccessTools.Field(typeof(ItemView), "_border");
        static readonly FieldInfo Field_ItemView_ColorPanel = AccessTools.Field(typeof(ItemView), "ColorPanel");
        static readonly FieldInfo Field_ItemView_MainImage = AccessTools.Field(typeof(ItemView), "MainImage");
        //static readonly FieldInfo Field_ItemView_Caption = AccessTools.Field(typeof(ItemView), "Caption");

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(FastAccessGrenadeGridItemView), nameof(FastAccessGrenadeGridItemView.Create));
        }

        [PatchPostfix]
        private static void PatchPostfix(ref FastAccessGrenadeGridItemView __result)
        {
            Image imageBorder = Field_ItemView__border.GetValue(__result) as Image;
            if (imageBorder != null)
                imageBorder.enabled = false;

            Image imageColorPanel = Field_ItemView_ColorPanel.GetValue(__result) as Image;
            if (imageColorPanel != null)
                imageColorPanel.enabled = false;

            Image imageMainImage = Field_ItemView_MainImage.GetValue(__result) as Image;
            if (imageMainImage != null)
                imageMainImage.color = new Color(1, 1, 1, UNSELECTED_ALPHA);

            //TMP_Text caption = Field_ItemView_Caption.GetValue(__result) as TMP_Text;
            //caption.rectTransform.anchorMin = new Vector2(0, 0);
            //caption.rectTransform.anchorMax = new Vector2(1, 0);
            //caption.rectTransform.pivot = new Vector2(0.5f, 0);
        }
    }

    internal class Patch_FastAccessGrenadeItemView_Awake : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(FastAccessGrenadeItemView), nameof(FastAccessGrenadeItemView.Awake));
        }

        [PatchPostfix]
        private static void PatchPostfix(FastAccessGrenadeItemView __instance)
        {
            __instance.gameObject.transform.SetAsLastSibling();
        }
    }

    class Patch_FastAccessGrenadeGridItemView_Highlight : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(FastAccessGrenadeGridItemView), nameof(FastAccessGrenadeGridItemView.Highlight));
        }

        [PatchPostfix]
        private static void PatchPostfix(FastAccessGrenadeGridItemView __instance, Image ___MainImage, bool highlight)
        {
            ___MainImage.color = new Color(1, 1, 1, highlight ? SELECTED_ALPHA : UNSELECTED_ALPHA);
        }
    }
}
