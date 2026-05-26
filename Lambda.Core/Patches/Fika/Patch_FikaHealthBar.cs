using BepInEx.Configuration;
using Fika.Core;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Core.Patches;

internal class Patch_FikaConfig_ToNumber : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(FikaConfig), nameof(FikaConfig.UseNamePlates));

    [PatchPostfix]
    static void Postfix(ref ConfigEntry<bool> __result)
    {
        __result.Value = false;
    }
}