using EFT.Interactive;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_LootItem_Init : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(LootItem), nameof(LootItem.Init));

    [PatchPrefix]
    static bool Prefix(string itemName)
    {
        if (itemName == "Corpse") return false;

        return true;
    }
}
