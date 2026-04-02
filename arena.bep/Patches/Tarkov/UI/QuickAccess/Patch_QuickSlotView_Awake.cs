using EFT;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
{
    internal class Patch_QuickSlotView_Awake : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(QuickSlotView), nameof(QuickSlotView.Awake));
        }

        [PatchPostfix]
        private static void PatchPostfix(QuickSlotView __instance, Image ___Background, Image ____arrow)
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
                ____arrow.rectTransform.anchoredPosition = new Vector2(2, 0);
            }
        }

        static void DisableBGGraphic(Transform slotView)
        {
            DisableIfFound(slotView, "Dark Background");
            DisableIfFound(slotView, "Background");
            DisableIfFound(slotView, "Border");

            var installPlace = slotView.transform.Find("InstallPlace").GetComponent<Graphic>();
            if (installPlace != null && installPlace.TryGetComponent<Graphic>(out var graphic))
                graphic.enabled = false;
        }

        static void DisableIfFound(Transform parent, string findChild)
        {
            Transform foundChild = parent.transform.Find(findChild);
            if (foundChild != null)
                foundChild.gameObject.SetActive(false);
        }
    }
}
