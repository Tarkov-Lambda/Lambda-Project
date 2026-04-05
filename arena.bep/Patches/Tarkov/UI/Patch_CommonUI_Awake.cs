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

            StretchInventoryScreen(__instance);
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
            vlg.spacing = -10;

            vlg = quickSlots.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;
            vlg.padding.left = 5;
            vlg.spacing = 20;

            quickAccessPanel.RectTransform.pivot = new Vector2(0, 0);
            quickAccessPanel.RectTransform.anchorMin = new Vector2(0, 0);
            quickAccessPanel.RectTransform.anchorMax = new Vector2(0, 0);
            quickAccessPanel.RectTransform.anchoredPosition = new Vector2(15, 70);
        }

        public static void StretchInventoryScreen(CommonUI commonUI)
        {
           Transform itemsPanel = commonUI.InventoryScreen.transform.Find("Items Panel");
            RectTransform leftSide = itemsPanel.Find("LeftSide") as RectTransform;
            leftSide.offsetMin = new Vector2(leftSide.offsetMin.x, 40f);
            RectTransform stashPanel = itemsPanel.Find("Stash Panel") as RectTransform;
            stashPanel.offsetMin = new Vector2(stashPanel.offsetMin.x, 40f);
        }
    }
}