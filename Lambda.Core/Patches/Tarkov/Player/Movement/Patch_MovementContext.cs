using Comfort.Common;
using EFT;
using HarmonyLib;
using Lambda.Core.Main;
using Lambda.Core.Networking;
using Lambda.Core.Main.MovementStates;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_MovementContext_CanWalk : ModulePatch
{
    private static bool wasOnLadder = false;

    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.CanWalk));

    [PatchPostfix]
    static void Postfix(MovementContext __instance, Player ____player, ref bool __result)
    {
        if (!H.IsArenaReady) return;
        if (!____player.IsYourPlayer) return;

        if (____player.Context.IsControllerPartiallyLocked())
        {
            __result = false;
            return;
        }

        if (LadderManager.isOnLadder)
        {
            wasOnLadder = true;
            __result = false;
        }
        else if (wasOnLadder && !H.MainPlayer.MovementContext.IsGrounded) // wait until the player is on the ground after ladder use to move 
        {
            wasOnLadder = false;
            __result = false;
        }
        else if (Noclip.IsEnabled)
        {
            __result = false;
        }
        else
        {
            __result = true;
        }
    }
}

internal class Patch_MovementContext_CanJump : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.CanJump));

    [PatchPostfix]
    static void Postfix(MovementContext __instance, Player ____player, ref bool __result)
    {
        if (!H.IsArenaReady) return;
        if (!____player.IsYourPlayer) return;

        if (____player.Context.IsControllerPartiallyLocked())
        {
            __result = false;
            return;
        }

        if (Noclip.IsEnabled)
        {
            __result = false;
            return;
        }
        else
        {
            __result = true;
        }
    }
}

internal class Patch_MovementContext_CanProne : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.CanProne));

    [PatchPostfix]
    static void Postfix(MovementContext __instance, Player ____player, ref bool __result)
    {
        if (!H.IsArenaReady) return;
        __result = false;
    }
}


// BLINDFIRE
internal class Patch_MovementContext_PlayerAnimatorSetBlindFire : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.PlayerAnimatorSetBlindFire));

    [PatchPrefix]
    private static bool Prefix() => false;
}

internal class Patch_MovementContext_SetBlindFire : ModulePatch
{
    private static int lastSentState;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.SetBlindFire), [typeof(int)]);

    [PatchPrefix]
    private static bool Prefix(Player ____player, MovementContext __instance, int b)
    {
        if (____player.MovementContext.CurrentState is SprintStateClass) return true;
        if (____player != null && ____player.HandsController != null)
        {
            ____player.HandsController.BlindFire(b);
            if (____player.IsYourPlayer && lastSentState != b)
            {
                Singleton<BlindFirePacketWarden>.Instance?.Send(b);
                lastSentState = b;
            }

        }
        return false;
    }
}

internal class Patch_MovementContext_ManualUpdate : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.ManualUpdate));

    [PatchPostfix]
    private static void Prefix(MovementContext __instance, Player ____player, float deltaTime)
    {
        if (!____player.Physical.Sprinting)
        {
            float clampedSpeed = __instance.ClampedSpeed;
            float num = Math.Abs(__instance.SmoothedCharacterMovementSpeed - clampedSpeed);
            if (num < 1E-45f)
            {
                return;
            }
            if (num > 0.001f)
            {
                __instance.SmoothedCharacterMovementSpeed = Mathf.Lerp(__instance.SmoothedCharacterMovementSpeed, clampedSpeed, deltaTime * EFTHardSettings.Instance.CHARACTER_SPEED_CHANGING_SPEED);
                return;
            }
            __instance.SmoothedCharacterMovementSpeed = clampedSpeed;
        }

        return;
    }
}

internal class Patch_MovementContext_GetNewState : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.GetNewState));

    [PatchPrefix]
    private static bool Prefix(MovementContext __instance, ref BaseMovementState __result, EPlayerState name, bool isAI = false)
    {
        if (name == EPlayerState.Run && !isAI)
        {
            __result = new UnstaggeredRunState(__instance);
            return false;
        }
        else if (name == EPlayerState.Plant && !isAI)
        {
            __result = new BetterPlantStateClass(__instance);
            return false;
        }

        return true;
    }
}

internal class Patch_MovementContext_SetAimingSlowdown : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.SetAimingSlowdown));

    [PatchPrefix]
    private static bool Prefix(MovementContext __instance, bool isAiming, ref float slow)
    {
        slow *= GameplayVariables.vars.AimSpeedPenaltyReduction;
        return true;
    }
}

internal class Patch_MovementContext_method_15 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.method_15));

    [PatchPrefix]
    private static bool Prefix(MovementContext __instance, ref float smoothDiff, ref float deltaTime)
    {
        deltaTime *= GameplayVariables.vars.LeanSpeed;
        // __instance.method_14(smoothDiff, deltaTime);
        return true;
    }
}

internal class Patch_MovementContext_ApplyDamageByVaulting : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.ApplyDamageByVaulting));

    [PatchPrefix]
    private static bool Prefix() => false;
}