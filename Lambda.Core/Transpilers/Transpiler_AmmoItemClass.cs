using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using EFT.Ballistics;
using HarmonyLib;
using SPT.Reflection.Patching;

public class Transpiler_AmmoItemClass_RicochetChance : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(AmmoItemClass), nameof(AmmoItemClass.RicochetChance));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

public class Transpiler_AmmoItemClass_FragmentationChance : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(AmmoItemClass), nameof(AmmoItemClass.FragmentationChance));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

public class Transpiler_AmmoItemClass_PenetrationChanceObstacle : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(AmmoItemClass), nameof(AmmoItemClass.PenetrationChanceObstacle));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
        yield return new CodeInstruction(OpCodes.Ret);
    }
}