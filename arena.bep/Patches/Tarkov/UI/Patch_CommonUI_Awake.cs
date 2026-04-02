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

            HorizontalLayoutGroup horizontalLayoutGroup = quickAccessPanel.GetComponent<HorizontalLayoutGroup>();
            if (horizontalLayoutGroup == null) // marker that we already modified
            {
                return;
            }
            Component.DestroyImmediate(horizontalLayoutGroup);

            var verticalLayoutGroup = quickAccessPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            quickAccessPanel.gameObject.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            quickAccessPanel.gameObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Transform weapon = quickAccessPanel.transform.Find("Weapon");
            Transform quickSlots = quickAccessPanel.transform.Find("QuickSlots");

            Component.DestroyImmediate(weapon.gameObject.GetComponent<HorizontalLayoutGroup>());
            Component.DestroyImmediate(quickSlots.gameObject.GetComponent<HorizontalLayoutGroup>());

            var vlg = weapon.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;

            vlg = quickSlots.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;

            quickAccessPanel.RectTransform.pivot = new Vector2(0, 0);
            quickAccessPanel.RectTransform.anchorMin = new Vector2(0, 0);
            quickAccessPanel.RectTransform.anchorMax = new Vector2(0, 0);
            quickAccessPanel.RectTransform.anchoredPosition = new Vector2(30, 170);
        }
    }
}