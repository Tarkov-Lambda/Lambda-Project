using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

public class Patch_ClientFirearmController_CanPressTrigger : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(ClientFirearmController), nameof(ClientFirearmController.IsTriggerPressed));

    [PatchPrefix]
    static bool Prefix(Player ____player, ref bool __result)
    {
        if (!H.IsInRaid()) return true;
        if (____player.GetContext().IsControllerPartiallyLocked())
        {
            __result = false;
            return false;
        }

        return true;
    }
}


public class Patch_FirearmController_SetTriggerPressed : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.SetTriggerPressed));

    [PatchPrefix]
    static void Prefix(Player.FirearmController __instance, Player ____player, ref bool pressed)
    {
        if (!H.IsInRaid()) return;
        if (____player.GetContext().IsControllerPartiallyLocked())
        {
            pressed = false;
        }
    }
}