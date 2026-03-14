using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.UI;
using Fika.Core.Main.Components;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov
{
    public class Patch_CanWalk : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.CanWalk));
        }

        [PatchPostfix]
        static void Postfix(ref bool __result)
        {
            if (!H.isInRaid()) return;
            if (Singleton<ArenaController>.Instance.session.IsControllerPartiallyLocked())
            {
                __result = false;
            }
        }
    }

    public class Patch_CanJump : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.CanJump));
        }

        [PatchPostfix]
        static void Postfix(ref bool __result)
        {
            if (!H.isInRaid()) return;
            if (Singleton<ArenaController>.Instance.session.IsControllerPartiallyLocked())
            {
                __result = false;
            }
        }
    }

    public class Patch_Inertia : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.Inertia));
        }

        [PatchPostfix]
        static void Postfix(ref float __result)
        {
            __result = 0f;
        }
    }

    public class Patch_WalkInertia : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.WalkInertia));
        }

        [PatchPostfix]
        static void Postfix(ref float __result)
        {
            __result = 0f;
        }
    }

    public class Patch_UpdateSprintInertia : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MovementContext), nameof(MovementContext.UpdateSprintInertia));
        }

        [PatchPrefix]
        static bool Prefix(MovementContext __instance)
        {
            __instance.PlayerAnimator_1.SetSprintInertia(0f);
            return false;
        }
    }

    public class Patch_SprintBrakeInertia : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.SprintBrakeInertia));
        }

        [PatchPostfix]
        static void Postfix(ref float __result)
        {
            __result = 0f;
        }
    }

    public class Patch_InstantAcceleration : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MovementContext), nameof(MovementContext.SetCharacterMovementSpeed));
        }

        [PatchPrefix]
        static void Prefix(ref bool force)
        {
            // Setting 'force' to true bypasses the SmoothedCharacterMovementSpeed lerp,
            // resulting in instant W/A/S/D acceleration and deceleration.
            force = true;
        }
    }
    // 1. Bypass Root Motion completely and inject raw, instantaneous Vector movement
    public class Patch_DirectApplyMotion : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MovementContext), nameof(MovementContext.DirectApplyMotion));
        }

        [PatchPrefix]
        static void Prefix(MovementContext __instance, ref Vector3 motion, float deltaTime, Player ____player)
        {
            // Only apply this to the local player so we don't break AI or other networked players
            if (____player == null || __instance.IsAI || !____player.IsYourPlayer) return;

            // Only override when on the ground and not locked to a stationary weapon
            if (__instance.IsGrounded && !__instance.IsInMountedState && !__instance.IsStationaryWeaponInHands)
            {
                Vector2 input = ____player.InputDirection;
                float originalY = motion.y; // Preserve gravity and falling

                // If no keys are pressed, stop DEAD instantly
                if (input.sqrMagnitude < 0.01f)
                {
                    motion.x = 0f;
                    motion.z = 0f;

                    // Allow Weapon wall-pushback to still function so we don't clip walls
                    if (____player.POM != null)
                    {
                        Vector3 pom = ____player.POM.GetOffsetXZ();
                        motion.x += pom.x;
                        motion.z += pom.z;
                    }
                    return;
                }

                // Determine precise speed based on stance (Crouching, Walking, Sprinting)
                float speed = __instance.ClampedSpeed;
                if (__instance.IsSprintEnabled && input.y > 0)
                {
                    speed = __instance.SprintingSpeed * __instance.StateSprintSpeedLimit;
                }

                // Calculate the true 3D direction relative to where the camera is looking
                Vector3 forward = __instance.PlayerRealForward;
                Vector3 right = __instance.PlayerRealRight;
                Vector3 desiredMove = (right * input.x + forward * input.y).normalized;

                // OVERRIDE horizontal root motion with direct mathematical velocity
                motion = desiredMove * (speed * deltaTime);
                motion.y = originalY;

                // Apply Weapon wall-pushback
                if (____player.POM != null)
                {
                    Vector3 pom = ____player.POM.GetOffsetXZ();
                    motion.x += pom.x;
                    motion.z += pom.z;
                }
            }
        }
    }

    // 2. Nuke the "Plant" (Pivot/Brake) State
    public class Patch_DisablePlantState : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MovementContext), nameof(MovementContext.GetNewState));
        }

        [PatchPrefix]
        static bool Prefix(ref EPlayerState name)
        {
            // If the game tries to load the pivot/braking state, trick it into loading the standard Run state instead
            if (name == EPlayerState.Plant)
            {
                name = EPlayerState.Run;
            }
            return true;
        }
    }

    // 3. Disable Animator's internal inertia dampening
    public class Patch_DisableAnimatorInertia : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MovementContext), nameof(MovementContext.PlayerAnimatorEnableInert));
        }

        [PatchPrefix]
        static void Prefix(ref bool enabled)
        {
            enabled = false;
        }
    }

    // 4. Remove Speed Blending/Lerping
    public class Patch_InstantSpeed : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MovementContext), nameof(MovementContext.SetCharacterMovementSpeed));
        }

        [PatchPrefix]
        static void Prefix(ref bool force)
        {
            // Forces the game to instantly hit target speed instead of ramping up
            force = true;
        }
    }

    // 5. Defeat lingering weight-based inertia variables
    public class Patch_WeightRelatedValuesUpdated : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MovementContext), nameof(MovementContext.WeightRelatedValuesUpdated));
        }

        [PatchPostfix]
        static void Postfix(MovementContext __instance)
        {
            __instance.WalkInertia = 0f;
            __instance.SprintBrakeInertia = 0f;
            __instance.TiltInertia = 0f;
            __instance._poseInertia = 0f;
            __instance._currentPoseInertia = 0f;

            if (__instance.PlayerAnimator_1 != null)
            {
                __instance.PlayerAnimator_1.SetSprintInertia(0f);
            }
        }
    }
}
