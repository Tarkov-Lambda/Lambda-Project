using EFT;
using EFT.Animations;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_ProceduralWeaponAnimation_ProcessEffectors : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ProceduralWeaponAnimation), nameof(ProceduralWeaponAnimation.ProcessEffectors));

    public static AccessTools.FieldRef<Player.FirearmController, Player> playerRef = AccessTools.FieldRefAccess<Player.FirearmController, Player>("_player");

    [PatchPrefix]
    static bool Postfix(ProceduralWeaponAnimation __instance, Player.FirearmController ____firearmController, ref Vector3 motion, ref Vector3 velocity)
    {
        if (!H.IsArenaReady) return true;
        if (____firearmController is null) return true;
        if (____firearmController.Item is not PistolItemClass) return true;

        if (__instance.IsAiming)
        {
            motion *= GameplayVariables.vars.PistolADSMotionScale;
            velocity *= GameplayVariables.vars.PistolADSMotionScale;
            __instance.Mask &= ~EProceduralAnimationMask.Walking; // no bobbing effect
        }
        else
        {
            if (playerRef(____firearmController).MovementContext.CurrentState is RunStateClass and not SprintStateClass)
            {
                __instance.Mask |= EProceduralAnimationMask.Walking;
            }
        }

        return true;
    }
}

internal class Patch_ProceduralWeaponAnimation_UpdateSwayFactors : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ProceduralWeaponAnimation), nameof(ProceduralWeaponAnimation.UpdateSwayFactors));

    [PatchPostfix]
    static void Postfix(ProceduralWeaponAnimation __instance, Player.FirearmController ____firearmController,
    ref float ____displacementStr,
    ref float ____swayStrength,
    ref float ____aimSwayStrength)
    {
        if (!H.IsArenaReady) return;
        if (____firearmController is null) return;

        if (____firearmController.Item is SniperRifleItemClass or MarksmanRifleItemClass)
        {
            __instance.AimingDisplacementStr = 0f;
            __instance.MotionReact.SwayFactors = Vector3.zero;

            ____displacementStr = 0f;
            ____swayStrength = 0f;
            ____aimSwayStrength = 0f;
        }
        else if (____firearmController.Item is PistolItemClass)
        {
            __instance.AimingDisplacementStr *= GameplayVariables.vars.PistolDisplacementStrScale;
            __instance.MotionReact.SwayFactors *= GameplayVariables.vars.PistolDisplacementStrScale;

            ____displacementStr *= GameplayVariables.vars.PistolDisplacementStrScale;
            ____swayStrength *= GameplayVariables.vars.PistolDisplacementStrScale;
            ____aimSwayStrength *= GameplayVariables.vars.PistolDisplacementStrScale;
        }
    }
}

internal class Patch_ProceduralWeaponAnimation_CalculateCameraPosition : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
    AccessTools.Method(typeof(ProceduralWeaponAnimation), nameof(ProceduralWeaponAnimation.CalculateCameraPosition));

    [PatchPostfix]
    static void Postfix(ProceduralWeaponAnimation __instance, Player.FirearmController ____firearmController, ref Vector3 ____vCameraTarget)
    {
        if (__instance.IsAiming)
        {
            if (____firearmController.Weapon is PistolItemClass)
            {
                ____vCameraTarget.z += GameplayVariables.vars.PistolZoomBoostScale;
            }
        }
    }
}


// Do blindfire procedure manually
internal class Patch_ProceduralWeaponAnimation_ZeroAdjustments : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ProceduralWeaponAnimation), nameof(ProceduralWeaponAnimation.ZeroAdjustments));

    [PatchPrefix]
    private static bool Prefix(
        ProceduralWeaponAnimation __instance,
        ref Vector3 ____blindFirePosition,
        ref Vector3 ____blindFireRotation,
        ref float ____blindfireStrength)
    {
        __instance.PositionZeroSum.y =
            __instance._shouldMoveWeaponCloser ? 0.05f : 0f;

        __instance.RotationZeroSum.y =
            __instance.SmoothedTilt * __instance.PossibleTilt;

        float value = __instance.BlindfireBlender.Value;
        float num = Mathf.Abs(value);

        if (num > 0f)
        {
            ____blindfireStrength = Mathf.Abs(__instance.Pitch) < 45f ? 1f : (90f - Mathf.Abs(__instance.Pitch)) / 45f;

            ____blindFirePosition = value > 0f ? __instance.BlindFireOffset * num : __instance.SideFireOffset * num;

            ____blindFireRotation = value > 0f ? __instance.BlindFireRotation * num : __instance.SideFireRotation * num;

            __instance.BlindFireEndPosition = value > 0f ? __instance.BlindFireOffset : __instance.SideFireOffset;

            __instance.BlindFireEndPosition *= ____blindfireStrength;
        }
        else
        {
            ____blindFirePosition = Vector3.zero;
            ____blindFireRotation = Vector3.zero;
            ____blindfireStrength = 0f;
        }

        __instance.HandsContainer.HandsPosition.Zero = __instance.PositionZeroSum + ____blindfireStrength * ____blindFirePosition;

        __instance.HandsContainer.HandsRotation.Zero = __instance.RotationZeroSum + ____blindFireRotation;

        return false;
    }
}