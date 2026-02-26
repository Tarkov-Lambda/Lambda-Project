using EFT;
using EFT.HealthSystem;
using Fika.Core.Main.Components;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep
{
    internal class pActiveHealthController_Kill : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EFT.HealthSystem.ActiveHealthController), nameof(EFT.HealthSystem.ActiveHealthController.Kill));
        }

        
        [PatchPrefix]
        static bool Prefix(ActiveHealthController __instance, EDamageType damageType)
        {
            if (!Plugin.Active.Value) return true;

            __instance.RestoreFullHealth();

            // Delayed healing to make sure every negative effect is fixed
            FixLater(__instance);

            Teleporter.Teleport(__instance.Player);
            return false;
        }

        static async void FixLater(ActiveHealthController __instance)
        {
            await Task.Delay(500);
            foreach (EBodyPart bodypart in Enum.GetValues(typeof(EBodyPart)))
            {
                __instance.RemoveNegativeEffects(bodypart);
            }
            __instance.RestoreFullHealth();

        }
    }
}
