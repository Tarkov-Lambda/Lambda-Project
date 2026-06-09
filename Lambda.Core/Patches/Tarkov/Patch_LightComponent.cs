using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_LightComponent_IsActive : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(LightComponent), nameof(LightComponent.SetLightState));

    [PatchPrefix]
    static bool Prefix(ref FirearmLightStateStruct state)
    {
        // D.Log(");
            state.IsActive = false;

        // if (H.IsNightTime)
        // {
        //     return false;
        // }
        return true;
    }
}
