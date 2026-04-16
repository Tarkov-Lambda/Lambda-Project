using EFT;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess.MagazineSelector
{
    internal class Patch_AmmoSelector_Position : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(AmmoSelector), nameof(AmmoSelector.ShowAcceptableMags));
        }

        [PatchPrefix]
        private static void PatchPrefix(AmmoSelector __instance, ref Vector2 ____arrowOffset)
        {
            ____arrowOffset = new Vector2(200,50);
        }
    }
}
