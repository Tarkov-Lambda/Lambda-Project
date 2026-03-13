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
using EFT.InventoryLogic;
using System.Linq;

namespace ifp.arena.bep.Patches.Tarkov
{
    public class Patch_GClass3125_CanAcceptRaid : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GClass3125), nameof(GClass3125.CanAcceptRaid));
        }

        [PatchPrefix]
        static bool Prefix(ref bool __result, GClass3125 __instance, out InventoryError error)
        {
            error = null;
            if (ItemsUtils.OverridableSlots.Contains(__instance.ID))
            {
                __result = true;
                return false;
            }
            else
            {
                return true;
            }

        }
    }
}
