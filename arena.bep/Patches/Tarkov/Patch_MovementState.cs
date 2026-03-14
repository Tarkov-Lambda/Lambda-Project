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
    // Allow Blind Fire whilst running
    public class Patch_MovementState_BlindFire : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // Target the base MovementState which drops the input by default
            return typeof(MovementState).GetMethod("BlindFire", BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static bool Prefix(MovementState __instance, int b)
        {
            // Force the input to go through to the MovementContext regardless of current movement state
            if (__instance.MovementContext != null)
            {
                __instance.MovementContext.SetBlindFire(b);
            }

            return false; // Skip original empty method
        }
    }
}
