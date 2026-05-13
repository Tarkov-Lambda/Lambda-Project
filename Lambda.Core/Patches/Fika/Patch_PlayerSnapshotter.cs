using BepInEx.Configuration;
using Fika.Core;
using Fika.Core.Main.Components;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.Packets.Player;
using Fika.Core.Networking.Snapshotting;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Lambda.Core.Patches;

// crank send rate to 60hz
internal class Patch_FikaGlobals_ToNumber : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(FikaGlobals), nameof(FikaGlobals.ToNumber));

    [PatchPostfix]
    void Postfix(ref int __result)
    {
        __result = 60;
    }
}


// Reduce interpolation base delay
internal class Patch_AdaptiveJitterBuffer_CurrentDelay : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(AdaptiveJitterBuffer), nameof(AdaptiveJitterBuffer.CurrentDelay));
    const double BASE_DELAY = 0.02;
    const double MAX_DELAY  = 0.25;

    [PatchPostfix]
    bool Prefix(AdaptiveJitterBuffer __instance, double ____currentJitter, ref double __result)
    {
        __result = Math.Clamp(BASE_DELAY + ____currentJitter, BASE_DELAY, MAX_DELAY);
        return false;
    }
}

// snapshot capacity increase in ctor
internal class Patch_PlayerSnapshotter_Constructor : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Constructor(typeof(PlayerSnapshotter<PlayerStateSnapshot>));

    [PatchTranspiler]
    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => SnapshotterTranspilerUtility.Transpile(instructions);
    // /* 0x0001A1CE 1F0F         */ IL_0016: ldc.i4.s  15
    // /* 0x0001A25A 1F0F         */ IL_00A2: ldc.i4.s  15
}

// snapshot capacity increase in addsnapshot
internal class Patch_PlayerSnapshotter_AddSnapshot : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(PlayerSnapshotter<PlayerStateSnapshot>), nameof(PlayerSnapshotter<PlayerStateSnapshot>.AddSnapshot));

    [PatchTranspiler]
    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => SnapshotterTranspilerUtility.Transpile(instructions);
    // /* 0x0001A1CE 1F0F         */ IL_0016: ldc.i4.s  15
}

// snapshot capacity increase in GetInterpolationIndices
internal class Patch_PlayerSnapshotter_GetInterpolationIndices : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(PlayerSnapshotter<PlayerStateSnapshot>), nameof(PlayerSnapshotter<PlayerStateSnapshot>.GetInterpolationIndices));

    [PatchTranspiler]
    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => SnapshotterTranspilerUtility.Transpile(instructions);
    // /* 0x0001A33E 1F10         */ IL_004A: ldc.i4.s  16
    // /* 0x0001A34F 1F0F         */ IL_005B: ldc.i4.s  15
}

// PlayerSnapshotter<T> shared patching logic for const _capacity and _mask
internal static class SnapshotterTranspilerUtility
{
    const int NEW_CAPACITY = 64;
    const int NEW_MASK = NEW_CAPACITY - 1;

    public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_I4_S || instruction.opcode == OpCodes.Ldc_I4)
            {
                int val = Convert.ToInt32(instruction.operand);

                if (val == 16)
                {
                    instruction.operand = instruction.opcode == OpCodes.Ldc_I4_S ? (sbyte)NEW_CAPACITY : NEW_CAPACITY;
                }
                else if (val == 15)
                {
                    instruction.operand = instruction.opcode == OpCodes.Ldc_I4_S ? (sbyte)NEW_MASK : NEW_MASK;
                }
            }

            yield return instruction;
        }
    }
}
