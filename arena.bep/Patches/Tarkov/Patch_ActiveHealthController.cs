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
    public class Patch_ApplyDamage : ModulePatch
    {
        public static DamageInfoStruct LastReceivedDamageInfo { get; private set; }

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.ApplyDamage));
        }

        [PatchPrefix]
        static bool Postfix(ActiveHealthController __instance, EBodyPart bodyPart, float damage, DamageInfoStruct damageInfo)
        {
            LastReceivedDamageInfo = damageInfo;

            return true;
        }
    }

    public class Patch_Kill : ModulePatch
    {
        private static long _lastKillTime;
        private const int CooldownMs = 500;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.Kill));
        }

        
        [PatchPrefix]
        static bool Prefix(ActiveHealthController __instance, EDamageType damageType)
        {
            if (!Plugin.Active.Value) return true;
            if (__instance.Player.IsAI) return true;

            long now = Stopwatch.GetTimestamp();
            long elapsedMs = (now - _lastKillTime) * 1000 / Stopwatch.Frequency;

            if (elapsedMs < CooldownMs)
                return false;

            _lastKillTime = now;

            // Delayed double healing to make sure every negative effect is fixed
            FixMe(__instance);

            //Plugin.Logger.LogInfo(__instance.GetAllEffects().Where(iEffect => iEffect is ActiveHealthController.Painkiller);
  
            try
            {
                Singleton<ClientFirearmController>.Instance.SetAim(false);
                Singleton<PlayerKilledPacketHandler>.Instance.Send(Patch_ApplyDamage.LastReceivedDamageInfo.Player.iPlayer.Id, __instance.Player.Id, 1);
                Teleporter.Teleport(__instance.Player);
            }
            catch (Exception ex)
            {
               Plugin.Logger.LogError(ex);
            }


            return false;
        }

        public static async void FixMe(ActiveHealthController __instance)
        {
            __instance.RestoreFullHealth();
            await Task.Delay(500);
            foreach (EBodyPart bodypart in Enum.GetValues(typeof(EBodyPart)))
            {
                __instance.RemoveNegativeEffects(bodypart);
            }
            __instance.RestoreFullHealth();

        }
    }
}
