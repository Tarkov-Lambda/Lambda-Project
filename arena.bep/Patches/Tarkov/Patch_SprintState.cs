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
using ifp.arena.bep.Core.MovementStates;
using SPT.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov
{
    public class NostalgiaPatrolFixEnterPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SprintStateClass), nameof(SprintStateClass.Enter));

        [PatchPostfix]
        private static void PostFix(SprintStateClass __instance)
        {
            __instance.MovementContext.SetPatrol(true);
        }
    }

    public class NostalgiaPatrolFixExitPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SprintStateClass), nameof(SprintStateClass.Exit));

        [PatchPostfix]
        private static void PostFix(SprintStateClass __instance)
        {
            __instance.MovementContext.SetPatrol(false);
        }
    }
}
