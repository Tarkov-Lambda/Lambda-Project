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
    // Place for patching out damage application if the shooter is already dead.
    // On the server if a shooter headshots, instead of waiting for the victim to report that they are dead
    // the server preemptively will report death (via PlayerKilledPacket). -- (when I figure out how to actually do this correctly)
    // however, considering the server will broadcast any damage packet
    // the victim may also be shooting the original shooter.
    // This will result in non stop kill trading.
    //
    // Here we can theoretically check if the shooter is already dead not damage
    // ourselves, or at least tighen up the kill trade window.
    public class Patch_ApplyDamage : ModulePatch
    {
        public static DamageInfoStruct LastReceivedDamageInfo { get; private set; }

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.ApplyDamage));
        }

        [PatchPrefix]
        static bool Prefix(ref float __result, ActiveHealthController __instance, EBodyPart bodyPart, float damage, DamageInfoStruct damageInfo)
        {
            // PlayerScore shooter = H.GetPlayerScore(damageInfo.Player.iPlayer.Id);
            // if (shooter != null && !shooter.isAlive)
            // {
            //     __result = 0f;
            //     return false;
            // }

            LastReceivedDamageInfo = damageInfo;
            // H.Dump(damageInfo);
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

            if (!H.MainPlayerScore.isAlive) return false;

            // Delayed double healing to make sure every negative effect is fixed
            _ = PlayerUtils.FixMe();
            Singleton<ReplenishPacketHandler>.Instance.Send();
            try
            {
                H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
                Singleton<PlayerKilledPacketHandler>.Instance.Send(Patch_ApplyDamage.LastReceivedDamageInfo);
                Singleton<RagdollCreator>.Instance.CreateLocalPlayerRagdoll();

                _ = PlayerUtils.CloseEyes(true, false);
                Teleporter.Teleport(__instance.Player);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError(ex);
            }


            return false;
        }
    }
}
