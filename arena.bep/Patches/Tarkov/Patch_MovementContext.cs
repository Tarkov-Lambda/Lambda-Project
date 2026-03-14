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
}
