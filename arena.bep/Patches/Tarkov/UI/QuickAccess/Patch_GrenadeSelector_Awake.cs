using EFT;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess
{
    internal class Patch_GrenadeSelector_Awake : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GrenadeSelector), nameof(GrenadeSelector.Awake));
        }

        [PatchPostfix]
        private static void PatchPostfix(GrenadeSelector __instance, ref Vector2 ____arrowOffset)
        {
            ____arrowOffset = new Vector2(70, 70);
        }
    }
}
