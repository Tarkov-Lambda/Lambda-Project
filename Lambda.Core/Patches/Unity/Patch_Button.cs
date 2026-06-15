using EFT.Ballistics;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Linq;
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

internal class Patch_GameObject_TryGetComponent : ModulePatch
{
    private static int _callCount;
    private static DateTime _lastLog = DateTime.UtcNow;

    protected override MethodBase GetTargetMethod() => typeof(GameObject)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .First(m =>
            m.Name == nameof(GameObject.TryGetComponent) &&
            m.IsGenericMethodDefinition &&
            m.GetGenericArguments().Length == 1)
        .MakeGenericMethod(typeof(BallisticCollider));

    [PatchPostfix]
    static void Postfix()
    {
        _callCount++;

        var now = DateTime.UtcNow;

        if ((now - _lastLog).TotalSeconds >= 1)
        {
            D.Log($"TryGetComponent<BaseBallistic> called {_callCount} times in the last second");

            _callCount = 0;
            _lastLog = now;
        }
    }
}