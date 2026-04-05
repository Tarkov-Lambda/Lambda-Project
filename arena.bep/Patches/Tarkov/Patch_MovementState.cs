using Comfort.Common;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;

// Allow Blind Fire whilst running
public class Patch_MovementState_BlindFire : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementState), nameof(MovementState.BlindFire));

    [PatchPrefix]
    private static bool Prefix(MovementState __instance, int b)
    {
        if (__instance.MovementContext.CurrentState is SprintStateClass) return true;

        // Force the input to go through to the MovementContext regardless of current movement state
        if (__instance.MovementContext != null)
        {
            __instance.MovementContext.SetBlindFire(b);
        }

        return false; // Skip original empty method
    }
}