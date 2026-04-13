using EFT;
using EFT.UI.Ragfair;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ifp.arena.bep.Patches.Tarkov.UI.WeaponBuilds
{
    internal class Patch_SubcategoryView_Show : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SubcategoryView), nameof(SubcategoryView.Show));
        }

        [PatchPostfix]
        private static void PatchPostfix(SubcategoryView __instance, RagFairClass ragfair, NodeBaseView categoryView, NodeBaseView subcategoryView, EntityNodeClass node, EViewListType viewListType, EWindowType windowType, Dictionary<string, NodeBaseView> viewNodes)
        {
            if (viewListType != EViewListType.WeaponBuild)
                return;

            //__instance.GetComponent<Image>().color = Color.blue;
        }
    }
}
