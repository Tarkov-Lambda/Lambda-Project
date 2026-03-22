using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using Fika.Core;
using Fika.Core.Main.Utils;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System;
using System.Diagnostics;
using System.Reflection;

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
    public class Patch_ActiveHealthController_ApplyDamage : ModulePatch
    {
        public static DamageInfoStruct LastReceivedDamageInfo { get; private set; }

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.ApplyDamage));


        // Has to be prefix so that we can capture the damageInfo packet before Kill is invoked
        [PatchPrefix]
        static bool Prefix(ref float __result, ActiveHealthController __instance, EBodyPart bodyPart, float damage, DamageInfoStruct damageInfo)
        {
            if (__instance.Player.IsYourPlayer)
            {
                LastReceivedDamageInfo = damageInfo;
            }
            return true;
        }

        [PatchPostfix]
        static void Postfix(ref float __result, ActiveHealthController __instance)
        {

        }
    }

    public class Patch_ActiveHealthController_Kill : ModulePatch
    {
        private static long _lastKillTime;
        private const int CooldownMs = 500;

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.Kill));


        [PatchPrefix]
        static bool Prefix(ActiveHealthController __instance, EDamageType damageType)
        {
            if (__instance.Player.IsAI) return true;
            D.Log($"{__instance.Player.Profile.Nickname} died");

            long now = Stopwatch.GetTimestamp();
            long elapsedMs = (now - _lastKillTime) * 1000 / Stopwatch.Frequency;

            if (elapsedMs < CooldownMs) return false;

            _lastKillTime = now;

            if (!H.GetPlayerScore(__instance.Player.Id).isAlive) return false;
            H.GetPlayerScore(__instance.Player.Id).Kill();

            if (__instance.Player.IsYourPlayer)
            {
                HU.FixMe().Forget();
                Singleton<ReplenishPacketHandler>.Instance.Send();

                __instance.Player.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
                Singleton<RagdollCreator>.Instance.CreateLocalPlayerRagdoll();

                _ = PU.CloseEyes(true, true);
                Teleporter.Teleport(__instance.Player);
            }

            if (FikaBackendUtils.IsServer || Patch_ActiveHealthController_ApplyDamage.LastReceivedDamageInfo.Player.iPlayer.Id == 1)
            // if (__instance.Player.IsYourPlayer )
            {
                Singleton<PlayerKilledPacketHandler>.Instance.Send(Patch_ActiveHealthController_ApplyDamage.LastReceivedDamageInfo);
            }

            return false;
        }
    }
}
