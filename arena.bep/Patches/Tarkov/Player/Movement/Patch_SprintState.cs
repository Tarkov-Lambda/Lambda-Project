using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;

public class NostalgiaPatrolFixEnterPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SprintStateClass), nameof(SprintStateClass.Enter));

    [PatchPostfix]
    private static void PostFix(SprintStateClass __instance)
    {
        __instance.MovementContext.SetPatrol(true);
    }
}

public class NostalgiaPatrolFixExitPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SprintStateClass), nameof(SprintStateClass.Exit));

    [PatchPostfix]
    private static void PostFix(SprintStateClass __instance)
    {
        __instance.MovementContext.SetPatrol(false);
    }
}