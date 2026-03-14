using EFT;
using EFT.Animations;
using HarmonyLib;
using ifp.arena.bep.Core;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov
{
    // Do blindfire procedure manually
    public class Patch_ProceduralWeaponAnimation_ZeroAdjustments : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ProceduralWeaponAnimation), "ZeroAdjustments");
        }

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
            __instance.PositionZeroSum.y = (__instance._shouldMoveWeaponCloser ? 0.05f : 0f);
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
}