using EFT.HealthSystem;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ifp.arena.bep.Patches.Tarkov.UI
{
    internal class Patch_CommonUI_Awake : ModulePatch
    {
        public static event Action<CommonUI> OnAwake;

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(CommonUI), nameof(CommonUI.Awake));

        [PatchPostfix]
        static void Postfix(CommonUI __instance)
        {
            OnAwake?.Invoke(__instance);

            ModifyQuickAccessPanel(__instance);
        }

        public static void ModifyQuickAccessPanel(CommonUI commonUI)
        {
            InventoryScreenQuickAccessPanel quickAccessPanel = commonUI.EftBattleUIScreen.QuickAccessPanel;

            var layoutGroup = quickAccessPanel.GetComponent<HorizontalOrVerticalLayoutGroup>();
            Component.DestroyImmediate(layoutGroup);

            var verticalLayoutGroup = quickAccessPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            quickAccessPanel.gameObject.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            quickAccessPanel.gameObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Transform weapon = quickAccessPanel.transform.Find("Weapon");
            Transform quickSlots = quickAccessPanel.transform.Find("QuickSlots");

            Component.DestroyImmediate(weapon.gameObject.GetComponent<HorizontalOrVerticalLayoutGroup>());
            Component.DestroyImmediate(quickSlots.gameObject.GetComponent<HorizontalOrVerticalLayoutGroup>());

            var vlg = weapon.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;

            vlg = quickSlots.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;
            vlg.padding.left = 5;
            vlg.spacing = 30;

            quickAccessPanel.RectTransform.pivot = new Vector2(0, 0);
            quickAccessPanel.RectTransform.anchorMin = new Vector2(0, 0);
            quickAccessPanel.RectTransform.anchorMax = new Vector2(0, 0);
            quickAccessPanel.RectTransform.anchoredPosition = new Vector2(29, 180);
        }
    }
}