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
using OldTarkovMovement.MovementStates;
using SPT.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    // Bypass Player Animator
    public class Patch_MovementContext_PlayerAnimatorSetBlindFire : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(MovementContext).GetMethod("PlayerAnimatorSetBlindFire", BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static bool Prefix()
        {
            return false;
        }
    }

    public class Patch_MovementContext_SetBlindFire : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(MovementContext).GetMethod("SetBlindFire",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(int) },
                null);
        }

        [PatchPrefix]
        private static bool Prefix(MovementContext __instance, int b)
        {
            var playerField = typeof(MovementContext).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance);

            if (playerField != null)
            {
                Player player = playerField.GetValue(__instance) as Player;

                if (player != null && player.HandsController != null)
                {
                    player.HandsController.BlindFire(b);

                    if (player.IsYourPlayer)
                        Singleton<BlindFirePacketHandler>.Instance?.Send(player.Id, b);
                }

                return false; // Skip the original method
            }

            return true;
        }
    }

    public class NostalgiaPatrolFixEnterPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(SprintStateClass).GetMethod(nameof(SprintStateClass.Enter), BindingFlags.Public | BindingFlags.Instance);

        [PatchPostfix]
        private static void PostFix(SprintStateClass __instance)
        {
            __instance.MovementContext.SetPatrol(true);
        }
    }

    public class NostalgiaPatrolFixExitPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(SprintStateClass).GetMethod(nameof(SprintStateClass.Exit), BindingFlags.Public | BindingFlags.Instance);

        [PatchPostfix]
        private static void PostFix(SprintStateClass __instance)
        {
            __instance.MovementContext.SetPatrol(false);
        }
    }

    public class SmoothSpeedFix : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // Replace with the target method's type and name
            return typeof(MovementContext).GetMethod(nameof(MovementContext.ManualUpdate), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        private static void Prefix(MovementContext __instance, float deltaTime)
        {
            var playerField = typeof(MovementContext).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance);

            if (playerField != null)
            {
                Player player = playerField.GetValue(__instance) as Player;

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
            }

            return;
        }
    }

    public class AimingSlowdownPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // Replace with the target method's type and name
            return typeof(MovementContext).GetMethod("SetAimingSlowdown", BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static bool Prefix(MovementContext __instance)
        {
            try
            {

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"GenericPatch Prefix failed: {ex}");
                return true;
            }
        }
    }

    public class StateReplacer : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // Replace with the target method's type and name
            return typeof(MovementContext).GetMethod("GetNewState", BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static bool Prefix(MovementContext __instance, ref BaseMovementState __result, EPlayerState name, bool isAI = false)
        {
            var IsForModern = true;
            try
            {
                switch (name)
                {
                    case EPlayerState.Idle:
                        __result = new OldIdleState(__instance);
                        return false;
                    case EPlayerState.ProneIdle:
                        if (isAI)
                        {
                            return true;
                        }
                        __result = new OldProneIdleState(__instance);
                        return false;
                    case EPlayerState.Run:
                        __result = new OldRunState(__instance);
                        return false;
                    case EPlayerState.Sprint:
                        if (IsForModern)
                        {
                            return true;
                        }

                        __result = new OldSprintState(__instance);

                        return false;
                    case EPlayerState.Jump:
                        if (IsForModern)
                        {
                            return true;
                        }

                        __result = new OldJumpState(__instance);
                        return false;
                    case EPlayerState.Sidestep:
                        __result = new OldSidestepState(__instance);
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"GenericPatch Prefix failed: {ex}");
                return true;
            }
        }
    }
}
