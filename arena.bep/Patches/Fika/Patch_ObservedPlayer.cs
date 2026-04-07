using Comfort.Common;
using EFT;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using EFT.HealthSystem;

namespace ifp.arena.bep.Patches
{
    internal sealed class Patch_ObservedPlayer_HandleDamagePacket : ModulePatch
    {
        private static readonly AccessTools.FieldRef<ObservedPlayer, DamageInfoStruct> LastDamageInfoRef = AccessTools.FieldRefAccess<ObservedPlayer, DamageInfoStruct>("LastDamageInfo");
        private static readonly AccessTools.FieldRef<ObservedPlayer, EBodyPart> LastBodyPartRef = AccessTools.FieldRefAccess<ObservedPlayer, EBodyPart>("LastBodyPart");
        private static readonly AccessTools.FieldRef<ObservedPlayer, EBodyPart> LastDamagedBodyPartRef = AccessTools.FieldRefAccess<ObservedPlayer, EBodyPart>("LastDamagedBodyPart");
        private static readonly AccessTools.FieldRef<ObservedPlayer, IPlayer> LastAggressorRef = AccessTools.FieldRefAccess<ObservedPlayer, IPlayer>("LastAggressor");
        private static readonly AccessTools.FieldRef<ObservedPlayer, MongoID?> _lastWeaponIdRef = AccessTools.FieldRefAccess<ObservedPlayer, MongoID?>("_lastWeaponId");


        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ObservedPlayer), nameof(ObservedPlayer.HandleDamagePacket));

        [PatchPrefix]
        static void Prefix(ObservedPlayer __instance, DamagePacket packet)
        {
            DamageInfoStruct damageInfo = new()
            {
                Damage = packet.Damage,
                DamageType = packet.DamageType,
                BodyPartColliderType = packet.ColliderType,
                HitPoint = packet.Point,
                HitNormal = packet.HitNormal,
                Direction = packet.Direction,
                PenetrationPower = packet.PenetrationPower,
                BlockedBy = packet.BlockedBy,
                DeflectedBy = packet.DeflectedBy,
                ArmorDamage = packet.ArmorDamage
            };

            if (packet.SourceId.HasValue)
            {
                damageInfo.SourceId = packet.SourceId.Value;
            }

            if (packet.ProfileId.HasValue)
            {
                var player = Singleton<GameWorld>.Instance.GetAlivePlayerBridgeByProfileID(packet.ProfileId.Value);

                if (player != null)
                {
                    damageInfo.Player = player;
                    LastAggressorRef(__instance) = player.iPlayer;
                }

                _lastWeaponIdRef(__instance) = packet.WeaponId;
            }

            if (H.IsServer)
            {
                ActiveHealthController serverAuthoritativeHealthController = __instance.HealthController as ActiveHealthController;
                serverAuthoritativeHealthController.ApplyDamage(packet.BodyPartType, damageInfo.Damage, damageInfo);

                __instance.ShotReactions(damageInfo, packet.BodyPartType);
                __instance.ReceiveDamage(damageInfo.Damage, packet.BodyPartType, damageInfo.DamageType, packet.Absorbed, packet.Material);
            }
            else
            {
                __instance.ShotReactions(damageInfo, packet.BodyPartType);
                // clients don't apply damage and just wait for serverside healthsyncs
                __instance.ReceiveDamage(0f, packet.BodyPartType, damageInfo.DamageType, packet.Absorbed, packet.Material);
            }

            LastDamageInfoRef(__instance) = damageInfo;
            LastBodyPartRef(__instance) = packet.BodyPartType;
            LastDamagedBodyPartRef(__instance) = packet.BodyPartType;
        }

    }

    // internal sealed class Patch_ObservedPlayer_ApplyClientShot : ModulePatch
    // {
    //     protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ObservedPlayer), nameof(ObservedPlayer.ApplyClientShot));
    // }


    // ObservedPlayer.NetworkHealthController is null on server
    internal class ObservedPlayer_PauseAllEffectsOnPlayer_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ObservedPlayer).GetMethod(nameof(ObservedPlayer.PauseAllEffectsOnPlayer));
        }

        [PatchPrefix]
        public static bool Prefix(ObservedPlayer __instance)
        {
            // Return false (skip original) when NetworkHealthController is null, which happens on
            // the server where the controller is ServerAuthoritativeHealthController.
            return __instance.NetworkHealthController != null;
        }
    }

    // same shit as above
    internal class ObservedPlayer_UnpauseAllEffectsOnPlayer_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ObservedPlayer).GetMethod(nameof(ObservedPlayer.UnpauseAllEffectsOnPlayer));
        }

        [PatchPrefix]
        public static bool Prefix(ObservedPlayer __instance)
        {
            return __instance.NetworkHealthController != null;
        }
    }
}

