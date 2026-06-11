using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using EFT;
using Audio.SpatialSystem;
using Lambda.Audio.SteamIntegration.AudioRooms;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Lambda.Audio.SteamIntegration.Patches;

internal class Transpiler_SpatialAudioSystem_Update : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.Update));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

internal class Transpiler_SpatialAudioSystem_LateUpdate : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.LateUpdate));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

public class Transpiler_SpatialAudioSystem_ProcessSourceOcclusion_1 : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
    AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.ProcessSourceOcclusion),
    new[] { typeof(IPlayer), typeof(BetterSource), typeof(bool) });

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

public class Transpiler_SpatialAudioSystem_ProcessSourceOcclusion_2 : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
    AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.ProcessSourceOcclusion),
    new[] { typeof(GameObject), typeof(BetterSource), typeof(EOcclusionTest), typeof(float), typeof(Vector3), typeof(bool) });

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

public class Transpiler_SpatialAudioSystem_ProcessSourceOcclusion_3 : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
    AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.ProcessSourceOcclusion),
    new[] { typeof(BetterSource), typeof(EOcclusionTest), typeof(Vector3) });

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

public class Transpiler_SpatialAudioSystem_ListenerCurrentRoom : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.ListenerCurrentRoom));

    public static ISpatialAudioRoom GetLambdaRoomHelper() => LambdaAudioRoomController.Instance.audioRoom;

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Transpiler_SpatialAudioSystem_ListenerCurrentRoom), nameof(GetLambdaRoomHelper)));
        yield return new CodeInstruction(OpCodes.Ret);
    }
}