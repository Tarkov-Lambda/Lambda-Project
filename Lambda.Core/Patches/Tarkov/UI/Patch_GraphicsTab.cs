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

internal class Patch_GameGraphicsTab_MaxFramerateLobbyLimit : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(GameGraphicsClass), nameof(GameGraphicsClass.MaxFramerateLobbyLimit));

    [PatchPostfix]
    static void Postfix(ref float __result)
    {
        __result = 120f;
    }
}


internal class Patch_GameGraphicsTab_MaxFramerateGameLimit : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(GameGraphicsClass), nameof(GameGraphicsClass.MaxFramerateGameLimit));

    [PatchPostfix]
    static void Postfix(ref float __result)
    {
        __result = 345f;
    }
}
