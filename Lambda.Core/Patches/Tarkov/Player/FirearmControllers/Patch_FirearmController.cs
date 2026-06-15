using EFT;
using EFT.InventoryLogic;
using HarmonyLib;

using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_FirearmController_Drop : ModulePatch
{
    private const float DropAnimationSpeed = 1000f;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.Drop));

    [PatchPrefix]
    static bool Prefix(ref float animationSpeed, Action callback, bool fastDrop, Item nextControllerItem)
    {
        animationSpeed = DropAnimationSpeed;
        return true;
    }
}

internal class Patch_FirearmController_Spawn : ModulePatch
{
    private const float DropAnimationSpeed = 3f;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.Spawn));

    [PatchPrefix]
    static bool Prefix(ref float animationSpeed, Action callback)
    {
        animationSpeed = DropAnimationSpeed;
        return true;
    }
}

internal class Patch_FirearmController_SetTriggerPressed : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.SetTriggerPressed));

    [PatchPrefix]
    static void Prefix(Player.FirearmController __instance, Player ____player, ref bool pressed)
    {
        if (!____player.IsYourPlayer) return;
        if (!H.IsArenaReady) return;

        if (____player.Context.IsControllerPartiallyLocked() || H.Session.matchState is MatchState.None or MatchState.Pause)
        {
            pressed = false;
        }
    }
}

internal class Patch_FirearmController_TotalErgonomics : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(Player.FirearmController), nameof(Player.FirearmController.TotalErgonomics));

    [PatchPostfix]
    static void Postfix(Player.FirearmController __instance, ref float __result)
    {
        if (__instance.Item is SniperRifleItemClass) __result = 100f;
    }
}

internal class Patch_FirearmController_ErgonomicWeight : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(Player.FirearmController), nameof(Player.FirearmController.ErgonomicWeight));

    [PatchPostfix]
    static void Postfix(Player.FirearmController __instance, ref float __result)
    {
        if (__instance.Item is SniperRifleItemClass) __result = 1f;
    }
}