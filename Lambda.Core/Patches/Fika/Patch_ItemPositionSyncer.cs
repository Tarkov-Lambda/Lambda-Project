using EFT.Interactive;
using Fika.Core.Main.Components;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace Lambda.Core.Patches;

internal sealed class Patch_ItemPositionSyncer_FixedUpdate : ModulePatch
{
    private static readonly AccessTools.FieldRef<ItemPositionSyncer, ObservedLootItem> LootItemRef = AccessTools.FieldRefAccess<ItemPositionSyncer, ObservedLootItem>("_lootItem");

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ItemPositionSyncer), nameof(ItemPositionSyncer.FixedUpdate));

    [PatchPrefix]
    private static bool Prefix(ItemPositionSyncer __instance)
    {
        if (LootItemRef(__instance) == null)
        {
            Object.Destroy(__instance);
            return false;
        }
        return true;
    }
}

internal sealed class Patch_ItemPositionSyncer_NotifyDone : ModulePatch
{
    private static readonly AccessTools.FieldRef<ItemPositionSyncer, ObservedLootItem> LootItemRef = AccessTools.FieldRefAccess<ItemPositionSyncer, ObservedLootItem>("_lootItem");

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ItemPositionSyncer), "NotifyDone");

    [PatchPrefix]
    private static bool Prefix(ItemPositionSyncer __instance)
    {
        var lootItem = LootItemRef(__instance);
        if (lootItem == null || lootItem.ItemOwner == null)
        {
            Object.Destroy(__instance);
            return false;
        }
        return true;
    }
}
