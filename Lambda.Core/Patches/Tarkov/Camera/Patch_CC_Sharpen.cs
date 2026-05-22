using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using static EffectsController;

namespace Lambda.Core.Patches;


internal class Patch_Class640_method_1 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Class640), nameof(Class640.method_1));

    [PatchPrefix]
    public static bool Prefix(Class640 __instance)
    {
        __instance.Cc_Sharpen_0.MaskDesaturate = 0f;
        return false;
    }
}
