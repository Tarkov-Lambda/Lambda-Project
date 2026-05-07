using EFT;
using EFT.Animations;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov;

public class Patch_ProceduralWeaponAnimation_ProcessEffectors : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ProceduralWeaponAnimation), nameof(ProceduralWeaponAnimation.ProcessEffectors));

    [PatchPrefix]
    static bool Postfix(ProceduralWeaponAnimation __instance, Player.FirearmController ____firearmController, ref Vector3 motion, ref Vector3 velocity)
    {
        if (!H.IsInRaid()) return true;
        if (!Patch_Player_VisualPass.PwaToPlayer.TryGetValue(__instance, out Player player)) return true;
        if (player is null) return true;
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
            if (player.MovementContext.CurrentState is RunStateClass and not SprintStateClass)
            {
                __instance.Mask |= EProceduralAnimationMask.Walking;
            }
        }

        return true;
    }
}

public class Patch_ProceduralWeaponAnimation_UpdateSwayFactors : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ProceduralWeaponAnimation), nameof(ProceduralWeaponAnimation.UpdateSwayFactors));

    [PatchPostfix]
    static void Postfix(ProceduralWeaponAnimation __instance, Player.FirearmController ____firearmController,
    ref float ____displacementStr,
    ref float ____swayStrength,
    ref float ____aimSwayStrength)
    {
        if (!H.IsInRaid()) return;
        if (____firearmController is null) return;
        if (____firearmController.Item is not PistolItemClass) return;

        __instance.AimingDisplacementStr *= GameplayVariables.vars.PistolDisplacementStrScale;
        __instance.MotionReact.SwayFactors *= GameplayVariables.vars.PistolDisplacementStrScale;

        ____displacementStr *= GameplayVariables.vars.PistolDisplacementStrScale;
        ____swayStrength *= GameplayVariables.vars.PistolDisplacementStrScale;
        ____aimSwayStrength *= GameplayVariables.vars.PistolDisplacementStrScale;

    }
}

public class Patch_ProceduralWeaponAnimation_CalculateCameraPosition : ModulePatch
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
public class Patch_ProceduralWeaponAnimation_ZeroAdjustments : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ProceduralWeaponAnimation), nameof(ProceduralWeaponAnimation.ZeroAdjustments));

    [PatchPrefix]
    private static bool Prefix(ProceduralWeaponAnimation __instance)
    {
        var blindFirePositionField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_blindFirePosition");
        var blindFireRotationField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_blindFireRotation");
        var blindFireStrengthField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_blindfireStrength");

        if (blindFirePositionField == null || blindFireRotationField == null || blindFireStrengthField == null)
        {
            return true; // Continue to the original method if fields are not found
        }

        // Update PositionZeroSum and RotationZeroSum
        __instance.PositionZeroSum.y = __instance._shouldMoveWeaponCloser ? 0.05f : 0f;
        __instance.RotationZeroSum.y = __instance.SmoothedTilt * __instance.PossibleTilt;

        float value = __instance.BlindfireBlender.Value;
        float num = Mathf.Abs(value);

        float blindfireStrengthNew = 0f;

        if (num > 0f)
        {
            // Calculate blindfire strength
            blindfireStrengthNew = (Mathf.Abs(__instance.Pitch) < 45f) ? 1f : ((90f - Mathf.Abs(__instance.Pitch)) / 45f);

            blindFireStrengthField.SetValue(__instance, blindfireStrengthNew);

            // Update blindfire position
            Vector3 newPosition = (value > 0f) ? (__instance.BlindFireOffset * num) : (__instance.SideFireOffset * num);

            Vector3 newRotation = (value > 0f) ? (__instance.BlindFireRotation * num) : (__instance.SideFireRotation * num);

            blindFirePositionField.SetValue(__instance, newPosition);
            blindFireRotationField.SetValue(__instance, newRotation);

            __instance.BlindFireEndPosition = (value > 0f)
                ? __instance.BlindFireOffset
                : __instance.SideFireOffset;

            __instance.BlindFireEndPosition *= blindfireStrengthNew;
        }
        else
        {
            // Reset blindfire position and rotation
            blindFireRotationField.SetValue(__instance, Vector3.zero);
            blindFirePositionField.SetValue(__instance, Vector3.zero);
        }

        // Cast the blindfire position and rotation to Vector3
        Vector3 position = (Vector3)blindFirePositionField.GetValue(__instance);
        Vector3 rotation = (Vector3)blindFireRotationField.GetValue(__instance);

        // Update hands container positions and rotation
        __instance.HandsContainer.HandsPosition.Zero =
            __instance.PositionZeroSum +
            blindfireStrengthNew * position;

        __instance.HandsContainer.HandsRotation.Zero = __instance.RotationZeroSum + rotation;

        return false; // Skip the original method

    }
}