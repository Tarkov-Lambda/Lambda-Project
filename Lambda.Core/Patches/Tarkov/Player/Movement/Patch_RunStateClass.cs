using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

public class Patch_RunStateClass_Jump : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(RunStateClass), nameof(RunStateClass.Jump));

    [PatchPrefix]
    public static void Prefix(RunStateClass __instance)
    {
        if (__instance.MovementContext.PoseLevel > 0.6f)
        {
            __instance.MovementContext.TryJump();
        }

        // return false;
    }
}
