using Comfort.Common;
using EFT;
using EFT.Animations;
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov
{
    // For Patch_ProceduralWeaponAnimation_ProcessEffectors
    public class Patch_Player_VisualPass : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.VisualPass));

        public static readonly ConditionalWeakTable<ProceduralWeaponAnimation, Player> PwaToPlayer = new ConditionalWeakTable<ProceduralWeaponAnimation, Player>();
        
        [PatchPrefix]
        static void Prefix(Player __instance)
        {
            var pwa = __instance.ProceduralWeaponAnimation;
            if (pwa != null)
            {
                PwaToPlayer.Remove(pwa);
                PwaToPlayer.Add(pwa, __instance);
            }
        }
    }

    public class Patch_PlayerMove : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.Move));

        [PatchPostfix]
        static void Postfix(Player __instance, Vector2 direction)
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
        protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.MoveSideInertia));

        [PatchPostfix]
        static void Postfix(ref float __result)
        {
            __result = 0f;
        }
    }

    public class Patch_MoveDiagonalInertia : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.MoveDiagonalInertia));

        [PatchPostfix]
        static void Postfix(ref float __result)
        {
            __result = 0f;
        }
    }
}
