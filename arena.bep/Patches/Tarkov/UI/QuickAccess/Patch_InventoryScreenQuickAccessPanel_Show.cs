﻿using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess;

internal class Patch_InventoryScreenQuickAccessPanel_Show : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(InventoryScreenQuickAccessPanel), nameof(InventoryScreenQuickAccessPanel.Show),
            [typeof(InventoryController), typeof(ItemUiContext), typeof(GamePlayerOwner), typeof(InsuranceCompanyClass)]);

    [PatchPrefix]
    private static bool PatchPrefix(InventoryScreenQuickAccessPanel __instance)
    {
        return __instance.gameObject.GetComponent<BattleUIComponentAnimation>() != null;
    }
}