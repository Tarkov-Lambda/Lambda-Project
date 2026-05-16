using System;
using System.Reflection;
using System.Threading.Tasks;
using Bsg.GameSettings;
using EFT.UI;
using EFT.UI.Settings; // Make sure your references include where NumberSlider is located
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using static GClass1085;

namespace Lambda.Core.Patches.Tarkov.UI;

internal class Patch_GameSettingsTab_Show : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GameSettingsTab), nameof(GameSettingsTab.Show));

    [PatchPostfix]
    static void Postfix(GameSettingsTab __instance, GClass1085 gameSettings, NumberSlider ____fov, NumberSlider ____headbobbing, GClass1085 ___gclass1085_0)
    {
        SettingsTab.BindNumberSliderToSetting(____fov, ___gclass1085_0.FieldOfView, 50f, 80f);
        SettingsTab.BindNumberSliderToSetting(____headbobbing, ___gclass1085_0.HeadBobbing, 0.1f, 1f, "F1");
    }
}

internal class Patch_Class1841_method_0 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Class1841), nameof(Class1841.method_0));

    [PatchPrefix]
    static bool Prefix(int x, ref int __result)
    {
        __result = Mathf.Clamp(x, 50, 80);
        return false;
    }
}

internal class Patch_Class1841_method_1 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Class1841), nameof(Class1841.method_1));

    [PatchPrefix]
    static bool Prefix(float x, ref float __result)
    {
        __result = Mathf.Clamp(x, 0.1f, 1f);
        return false;
    }
}