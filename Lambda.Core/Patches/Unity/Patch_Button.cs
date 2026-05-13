using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Lambda.Core.Patches;


internal class Patch_Button_set_enabled : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(Button), nameof(Button.enabled));

    [PatchPrefix]
    public static bool Prefix(AudioSource __instance, ref bool value)
    {
        if (__instance.gameObject.name.StartsWith("JoinButton"))
        {
            value = true;
        }

        return true;
    }
}
