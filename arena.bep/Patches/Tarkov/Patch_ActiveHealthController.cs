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
    public class Patch_ApplyDamage : ModulePatch
    {
        public static DamageInfoStruct LastReceivedDamageInfo { get; private set; }

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.ApplyDamage));

        // Has to be prefix so that we can capture the damageInfo packet before Kill is invoked
        [PatchPrefix]
        static bool Prefix(ref float __result, ActiveHealthController __instance, EBodyPart bodyPart, float damage, DamageInfoStruct damageInfo)
        {
            LastReceivedDamageInfo = damageInfo;
            return true;
        }
    }

    public class Patch_Kill : ModulePatch
    {
        private static long _lastKillTime;
        private const int CooldownMs = 500;

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.Kill));


        [PatchPrefix]
        static bool Prefix(ActiveHealthController __instance, EDamageType damageType)
        {
            if (__instance.Player.IsAI) return true;

            long now = Stopwatch.GetTimestamp();
            long elapsedMs = (now - _lastKillTime) * 1000 / Stopwatch.Frequency;

            if (elapsedMs < CooldownMs)
                return false;

            _lastKillTime = now;

            if (!H.MainPlayerScore.isAlive) return false;

            // Delayed double healing to make sure every negative effect is fixed
            HU.FixMe().Forget();
            Singleton<ReplenishPacketHandler>.Instance.Send();
            try
            {
                H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
                
                // If the server player dies
                // or if the client kills themselves (explosion prolly, fall)
                if (FikaBackendUtils.IsServer || FikaBackendUtils.IsClient && Patch_ApplyDamage.LastReceivedDamageInfo.Player.iPlayer.Id == 1)
                {
                    D.Dump(Patch_ApplyDamage.LastReceivedDamageInfo);
                    Singleton<PlayerKilledPacketHandler>.Instance.Send(Patch_ApplyDamage.LastReceivedDamageInfo);
                }

                Singleton<RagdollCreator>.Instance.CreateLocalPlayerRagdoll();

                _ = PU.CloseEyes(true, false);
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
