using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SPT.Reflection.Patching;

internal class Transpiler_BetterSource_SetLowPassFilterParameters : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.SetLowPassFilterParameters));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

internal class Transpiler_BetterSource_SetHighPassFilterParameters : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.SetHighPassFilterParameters));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

internal class Transpiler_BetterSource_IncludeInOcclusionProcess : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.IncludeInOcclusionProcess));

    private static readonly FieldInfo IncludedInOcclusionProcess_FieldInfo = AccessTools.Field(typeof(BetterSource), "IncludedInOcclusionProcess");

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0);
        yield return new CodeInstruction(OpCodes.Ldc_I4_0);
        yield return new CodeInstruction(OpCodes.Stfld, IncludedInOcclusionProcess_FieldInfo);
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

public class Transpiler_BetterSource_SetOcclusionVolumeFactor : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.SetOcclusionVolumeFactor));

    private static readonly MethodInfo OcclusionVolumeFactor_MethodInfo = AccessTools.PropertySetter(typeof(BetterSource), nameof(BetterSource.OcclusionVolumeFactor));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0);
        yield return new CodeInstruction(OpCodes.Ldc_R4, 1f);
        yield return new CodeInstruction(OpCodes.Call, OcclusionVolumeFactor_MethodInfo);
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

internal class Transpiler_BetterSource_SetOcclusionRolloffScale : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.SetOcclusionRolloffScale));

    private static readonly MethodInfo OcclusionRolloffScale_MethodInfo = AccessTools.PropertySetter(typeof(BetterSource), nameof(BetterSource.OcclusionRolloffScale));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0);
        yield return new CodeInstruction(OpCodes.Ldc_R4, 1f);
        yield return new CodeInstruction(OpCodes.Call, OcclusionRolloffScale_MethodInfo);
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

internal class Patch_BetterSource_Init : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.Init));

    [PatchPostfix]
    static void Postfix(BetterSource __instance)
    {
        // BetterSourceProxyRouter.Attach(__instance);
    }
}