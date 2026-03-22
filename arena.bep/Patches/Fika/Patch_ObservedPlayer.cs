using Comfort.Common;
using EFT;
using Fika.Core.Main.HostClasses;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

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

            if (FikaBackendUtils.IsServer)
            {
                // 1. We are the Server. Feed the flesh damage into the Authoritative Controller.
                // This will calculate over-damage, bleeds, fractures, and death.
                ServerAuthoritativeHealthController serverAuthoritativeHealthController = __instance.HealthController as ServerAuthoritativeHealthController;
                serverAuthoritativeHealthController.ApplyDamage(packet.BodyPartType, damageInfo.Damage, damageInfo);

                // 2. Play visual/audio flinch reactions on the Server's proxy
                __instance.ShotReactions(damageInfo, packet.BodyPartType);
                __instance.ReceiveDamage(damageInfo.Damage, packet.BodyPartType, damageInfo.DamageType, packet.Absorbed, packet.Material);
            }
            else
            {
                // We are a Client (Either the Victim or a Bystander).
                // DO NOT call ApplyDamage here! We wait for the Server to send us HealthSyncPackets.

                // We DO still want to play the visual blood splatters and audio grunts locally:
                __instance.ShotReactions(damageInfo, packet.BodyPartType);
                __instance.ReceiveDamage(0f, packet.BodyPartType, damageInfo.DamageType, packet.Absorbed, packet.Material); // Notice damage is 0f so we don't accidentally lower HP early!
            }

            LastDamageInfoRef(__instance) = damageInfo;
            LastBodyPartRef(__instance) = packet.BodyPartType;
            LastDamagedBodyPartRef(__instance) = packet.BodyPartType;
        }

    }

    internal sealed class Patch_ObservedPlayer_CreateObservedPlayer : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ObservedPlayer), nameof(ObservedPlayer.CreateObservedPlayer));

        // replace observedhealthcontroller to serversidehealthcontroller (on server)
    }

        internal sealed class Patch_ObservedPlayer_ApplyClientShot : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ObservedPlayer), nameof(ObservedPlayer.ApplyClientShot));
    }
}
