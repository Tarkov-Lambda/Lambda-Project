using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess;

internal class PatchGroup_QuickAccessPanel_HideItemBG : PatchGroup
{
    private class Patch_QuickSlotView_Awake : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuickSlotView), nameof(QuickSlotView.Awake));
        }

        [PatchPostfix]
        private static void PatchPostfix(QuickSlotView __instance, Image ___Background, Image ____arrow, TMP_Text ___Caption, TMP_Text ___HotKey)
        {
            Transform mainWeapon = __instance.transform.Find("MainWeapon");
            if (mainWeapon != null)
            {
                DisableBGGraphic(mainWeapon);
            }
            else
            {
                DisableBGGraphic(__instance.transform);
            }

            if (____arrow != null)
            {
                ____arrow.rectTransform.anchorMin = new Vector2(1, 0.5f);
                ____arrow.rectTransform.anchorMax = new Vector2(1, 0.5f);
                ____arrow.rectTransform.eulerAngles = new Vector3(0, 0, 90);
                ____arrow.rectTransform.anchoredPosition = new Vector2(2, 13);
            }

            ___Caption.rectTransform.anchoredPosition = new Vector2(15f, 30f);
            ___Caption.horizontalAlignment = TMPro.HorizontalAlignmentOptions.Left;
            ___Caption.margin = new Vector4(6.5f, 0, 0, 0);

            ___HotKey.color = Color.white;
        }

        static void DisableBGGraphic(Transform slotView)
        {
            DisableIfFound(slotView, "Dark Background");
            DisableIfFound(slotView, "Background");
            DisableIfFound(slotView, "Border");

            var installPlace = slotView.transform.Find("InstallPlace");
            if (installPlace != null && installPlace.TryGetComponent<Graphic>(out var graphic))
                graphic.enabled = false;

            var bindPanel = slotView.transform.Find("Bind Panel");
            if (bindPanel != null && bindPanel.TryGetComponent<Graphic>(out var bindgraphic))
            {
                bindgraphic.enabled = false;
            }
        }

        static void DisableIfFound(Transform parent, string findChild)
        {
            Transform foundChild = parent.transform.Find(findChild);
            if (foundChild != null)
                foundChild.gameObject.SetActive(false);
        }
    }

    private class Patch_QuickSlotView_RefreshMalfunctionForWeapon : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuickSlotView), nameof(QuickSlotView.method_0));
        }

        [PatchPostfix]
        private static void PatchPostfix(QuickSlotView __instance, Image ___InstallPlace)
        {
            ___InstallPlace.enabled = false;
        }
    }
}
