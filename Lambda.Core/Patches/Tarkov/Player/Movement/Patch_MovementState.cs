using Comfort.Common;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

// Allow Blind Fire whilst running
internal class Patch_MovementState_BlindFire : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementState), nameof(MovementState.BlindFire));

    [PatchPrefix]
    private static bool Prefix(MovementState __instance, int b)
    {
        if (__instance.MovementContext.CurrentState is SprintStateClass) return true;
        __instance.MovementContext?.SetBlindFire(b);
        return false;
    }
}