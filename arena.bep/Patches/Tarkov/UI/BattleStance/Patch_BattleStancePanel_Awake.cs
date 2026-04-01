using EFT;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ifp.arena.bep.Patches.Tarkov.UI.BattleStance
{
    internal class Patch_BattleStancePanel_Awake : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BattleStancePanel), nameof(BattleStancePanel.Awake));
        }

        [PatchPostfix]
        private static void PatchPostfix(BattleStancePanel __instance, 
            List<EFT.UI.BattleStance> ____battleStances,
            Slider ____stanceSlider)
        {
            ____battleStances[0].StanceObject.transform.parent.gameObject.SetActive(false);

            ____stanceSlider.RectTransform().sizeDelta = new Vector2(20, 60);
        }
    }
}
