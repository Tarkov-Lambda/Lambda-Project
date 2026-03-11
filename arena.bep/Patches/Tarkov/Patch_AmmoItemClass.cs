using Comfort.Common;
using EFT;
using EFT.CameraControl;
using EFT.HealthSystem;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Comfort;
using UnityEngine;
using EFT.UI;

namespace ifp.arena.bep.Patches.Tarkov
{
    public class Patch_AmmoItemClass_RicochetChance : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(AmmoItemClass), nameof(AmmoItemClass.RicochetChance));
        }


        [PatchPrefix]
        static void Prefix(ref float __result)
        {
            __result = 0f;
        }
    }
}
