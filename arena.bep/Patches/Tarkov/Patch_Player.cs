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
    public class Patch_PlayerMove : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EFT.Player), nameof(EFT.Player.Move));
        }

        [PatchPostfix]
        static void Postfix(EFT.Player __instance, Vector2 direction)
        {
            // Only apply input snapping when walking/strafing, as sprinting 
            // uses a different forward-locked animation blend tree.
            if (__instance.MovementContext != null && !__instance.MovementContext.IsSprintEnabled)
            {
                __instance.MovementContext.MovementDirection = direction;
            }
        }
    }

    public class Patch_MoveSideInertia : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.MoveSideInertia));
        }

        [PatchPostfix]
        static void Postfix(ref float __result)
        {
            __result = 0f;
        }
    }

    public class Patch_MoveDiagonalInertia : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.MoveDiagonalInertia));
        }

        [PatchPostfix]
        static void Postfix(ref float __result)
        {
            __result = 0f;
        }
    }
}
