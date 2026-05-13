using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using Audio.SpatialSystem;
using Audio.Effects;

namespace Lambda.Core.Patches;

public class Patch_AudioBuiltinEQFilter_CalculateFrequency : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(AudioBuiltinEQFilter), nameof(AudioBuiltinEQFilter.InitializeComponents));

    [PatchPrefix]
    static bool Prefix() => false;
}