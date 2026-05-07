using Comfort.Common;
using EFT;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking;
using ifp.arena.bep.Core.MovementStates;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;
using static EFT.MovementContext;

namespace ifp.arena.bep.Patches.Tarkov;

public class Patch_MovementContext_CanWalk : ModulePatch
{
    private static bool wasOnLadder = false;

    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.CanWalk));

    [PatchPostfix]
    static void Postfix(MovementContext __instance, Player ____player, ref bool __result)
    {
        if (!H.IsInRaid()) return;

        if (H.MainPlayerScore.IsControllerPartiallyLocked())
        {
            __result = false;
        }

        if (____player.IsYourPlayer)
        {
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
        }

    }
}

public class Patch_MovementContext_CanJump : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.CanJump));

    [PatchPostfix]
    static void Postfix(MovementContext __instance, ref bool __result)
    {
        if (!H.IsInRaid()) return;
        if (H.MainPlayerScore.IsControllerPartiallyLocked())
        {
            __result = false;
        }
    }
}

// BLINDFIRE
public class Patch_MovementContext_PlayerAnimatorSetBlindFire : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.PlayerAnimatorSetBlindFire));

    [PatchPrefix]
    private static bool Prefix()
    {
        return false;
    }
}

public class Patch_MovementContext_SetBlindFire : ModulePatch
{
    private static int lastSentState;
    private static readonly AccessTools.FieldRef<MovementContext, Player> playerRef = AccessTools.FieldRefAccess<MovementContext, Player>("_player");

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.SetBlindFire), [typeof(int)]);

    [PatchPrefix]
    private static bool Prefix(MovementContext __instance, int b)
    {
        Player player = playerRef(__instance);

        if (player.MovementContext.CurrentState is SprintStateClass) return true;

        if (player != null && player.HandsController != null)
        {
            player.HandsController.BlindFire(b);

            if (player.IsYourPlayer && lastSentState != b)
            {
                Singleton<BlindFirePacketHandler>.Instance?.Send(b);
                lastSentState = b;
            }

        }
        return false;
    }
}

public class Patch_Class1396_method_3 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Class1396), nameof(Class1396.method_3));

    [PatchPrefix]
    private static bool Prefix(Class1396 __instance, Player player)
    {
        try
        {
            if (player.UsedSimplifiedSkeleton)
            {
                return false;
            }
            Quaternion handsRotation = player.HandsRotation;
            player.HandsController.ControllerGameObject.transform.SetPositionAndRotation(player.PlayerBones.Ribcage.Original.position, handsRotation);
            player.CameraContainer.transform.rotation = handsRotation;
        }
        catch (Exception)
        {
            D.Log("Error in MovementContext.method_3: This usually occurs when a player is trying to equip an item they are about to drop");
            if (player != null && player.IsYourPlayer)
            {
                player.UnfuckHands();
            }
        }
        return false;
    }
}

public class Patch_MovementContext_ManualUpdate : ModulePatch
{
    private static readonly AccessTools.FieldRef<MovementContext, Player> playerRef = AccessTools.FieldRefAccess<MovementContext, Player>("_player");

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.ManualUpdate));

    [PatchPostfix]
    private static void Prefix(MovementContext __instance, float deltaTime)
    {
        Player player = playerRef(__instance);

        if (!player.Physical.Sprinting)
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

public class Patch_MovementContext_GetNewState : ModulePatch
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

        return true;
    }
}

public class Patch_MovementContext_SetAimingSlowdown : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.SetAimingSlowdown));

    [PatchPrefix]
    private static bool Prefix(MovementContext __instance, bool isAiming, ref float slow)
    {
        slow *= GameplayVariables.vars.AimSpeedPenaltyReduction;
        return true;
    }
}

public class Patch_MovementContext_method_15 : ModulePatch
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

public class Patch_MovementContext_ApplyDamageByVaulting : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.ApplyDamageByVaulting));

    [PatchPrefix]
    private static bool Prefix() => false;
}