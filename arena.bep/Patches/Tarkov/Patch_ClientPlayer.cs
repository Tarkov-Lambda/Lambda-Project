using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using HarmonyLib;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov
{
    public class Patch_method181 : ModulePatch
    {
        private static long _lastKillTime;
        private const int CooldownMs = 500;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ClientPlayer), nameof(ClientPlayer.method_181));
        }

        
        [PatchPostfix]
        static void PostFix(ushort rttKey, int singleFixedUpdate, int serverTime)
        {
            Plugin.Logger.LogInfo($"{rttKey} {singleFixedUpdate} {serverTime}");
        }

    }
}
