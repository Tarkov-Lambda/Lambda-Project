using Comfort.Common;
using EFT;
using EFT.UI.Ragfair;
using HarmonyLib;
using Lambda.Core.Main;
using Lambda.Core.Main.Economy;
using ifp.arena.shared.Models;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Lambda.Core.Patches.Tarkov.UI.WeaponBuilds
{
    internal class Patch_CategoryView_Show : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(CategoryView), nameof(CategoryView.Show));
        }

        [PatchPrefix]
        private static void PatchPrefix(CategoryView __instance, RagFairClass ragfair, NodeBaseView categoryView, NodeBaseView subcategoryView, EntityNodeClass node, EViewListType viewListType, EWindowType windowType, Dictionary<string, NodeBaseView> viewNodes, string forbiddenItem)
        {
            if (viewListType != EViewListType.WeaponBuild)
                return;

            string bsgTemplateId = node.Id;
            if (bsgTemplateId.IsNullOrEmpty())
                return;


            bool isInEconomy = BuyMenuSelection.TryGetItemData(bsgTemplateId, out ShopItem shopItem);

            node.Data.BackgroundColor_1 = isInEconomy ? Color.blue : Color.clear;

            //Singleton<WeaponPresetManager>.Instanc.
        }
    }
}
