using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using Audio.ReverbSubsystem;
using UnityEngine;
using EFT;
using Audio.SpatialSystem;
using System.Collections.Generic;

namespace Lambda.Core.Patches;

internal class Patch_SpatialAudioSystem_Update : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.Update));

    [PatchPrefix]
    static bool Prefix() => false;
}


internal class Patch_SpatialAudioSystem_LateUpdate : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.LateUpdate));

    [PatchPrefix]
    static bool Prefix() => false;
}

public class Patch_SpatialAudioSystem_ListenerCurrentRoom : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.ListenerCurrentRoom));

    [PatchPrefix]
    static bool Prefix(ref ISpatialAudioRoom __result)
    {
        __result = LambdaAudioRoomController.Instance.audioRoom;
        return false;
    }
}

public class Patch_SpatialAudioSystem_ProcessSourceOcclusion_1 : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
    AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.ProcessSourceOcclusion),
    new[] { typeof(IPlayer), typeof(BetterSource), typeof(bool) });

    [PatchPrefix]
    static bool Prefix(ref int __result)
    {
        __result = -1;
        return false;
    }
}


public class Patch_SpatialAudioSystem_ProcessSourceOcclusion_2 : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
    AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.ProcessSourceOcclusion),
    new[] { typeof(GameObject), typeof(BetterSource), typeof(EOcclusionTest), typeof(float), typeof(Vector3), typeof(bool) });

    [PatchPrefix]
    static bool Prefix(ref int __result)
    {
        __result = -1;
        return false;
    }
}

public class Patch_SpatialAudioSystem_ProcessSourceOcclusion_3 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => 
    AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.ProcessSourceOcclusion),
    new[] { typeof(BetterSource), typeof(EOcclusionTest), typeof(Vector3) });

    [PatchPrefix]
    static bool Prefix(ref int __result)
    {
        __result = -1;
        return false;
    }
}
