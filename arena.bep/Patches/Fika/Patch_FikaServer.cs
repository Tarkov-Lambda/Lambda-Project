using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using Fika.Core.Main.Components;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Video;

namespace ifp.arena.bep.Patches
{

    internal sealed class Patch_FikaServer_OnCommonPlayerPacketReceived : ModulePatch
    {
        private static readonly AccessTools.FieldRef<FikaServer, CoopHandler> CoopHandlerRef = AccessTools.FieldRefAccess<FikaServer, CoopHandler>("_coopHandler");

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaServer), "OnCommonPlayerPacketReceived");

        [PatchPostfix]
        private static void Postfix(FikaServer __instance, CommonPlayerPacket packet, NetPeer peer)
        {
            // D.Log($"{peer.Id} sent {packet.GetType()} {packet.Type}");
            if (packet.Type != ECommonSubPacketType.Damage) return;
            if (packet.SubPacket is not DamagePacket damage) return;

            var coopHandler = CoopHandlerRef(__instance);
            int victimNetId = packet.NetId;

            if (!coopHandler.Players.TryGetValue(victimNetId, out var victim)) return;

            // we handle the server owner player in the Patch_Kill
            // kind of ass backwards, but it makes sense in my head rn
            if (victim.IsYourPlayer) return;


            DamageInfoStruct damageInfo = new()
            {
                Damage = damage.Damage,
                DamageType = damage.DamageType,
                BodyPartColliderType = damage.ColliderType,
                HitPoint = damage.Point,
                HitNormal = damage.HitNormal,
                Direction = damage.Direction,
                PenetrationPower = damage.PenetrationPower,
                // BlockedBy = packet.BlockedBy, // does not exist
                // DeflectedBy = packet.DeflectedBy, // does not exist
                ArmorDamage = damage.ArmorDamage
            };

            // Instead of waiting for healthsync, we apply a damage packet directly on the server on a player that's not ours.
            // I can't vouch as per how accurate this is going to be
            // but in theory this should be just fine, and if the client heals, they will send a healthsync packet later
            //
            // The only catch can be if the client sends a healthsync of their regened health right after we send this packet
            // shooter sends a packet of 60 damage to thorax of victim
            // victim sends a sync packet saying they just healed, right after we just send them a damage packet.
            // eventually the victim will be the source of truth when we healthsync, but just how serious is sync mismatch here given low ttk?
            // although, at the end of the day the victim will eventually pick up all the damage packets and apply them, in worst case scenario killing themselves (right?)
            ApplyDamage(victim, damage.BodyPartType, damage.Damage, damageInfo);

            victim.HandleDamagePacket(damage);
        }


        public static float ApplyDamage(FikaPlayer victim, EBodyPart bodyPart, float damage, DamageInfoStruct damageInfo)
        {
            if (!H.GetPlayerScore(victim.Id).isAlive) return 0f;

            ObservedHealthController healthController = victim.HealthController as ObservedHealthController;

            if (H.MainPlayer.ActiveHealthController.DamageMultiplier > 1f || bodyPart != EBodyPart.Head || !damageInfo.DamageType.IsEnemyDamage())
            {
                damage *= H.MainPlayer.ActiveHealthController.DamageMultiplier;
            }
            if (damageInfo.DamageType.IsEnvironmental())
            {
                damage *= GClass3009<ActiveHealthController.GClass3008>.GClass1728_0.ProfileHealthSettings.BodyPartsSettings[bodyPart].EnvironmentDamageMultiplier;
            }
            EDamageType damageType = damageInfo.DamageType;
            GClass3009<NetworkHealthControllerAbstractClass.NetworkBodyEffectsAbstractClass>.BodyPartState bodyPartState = healthController.Dictionary_0[bodyPart];
            float num = bodyPartState.Health.Current;
            float current = healthController.GetBodyPartHealth(EBodyPart.Common, false).Current;
            ChangeHealth(healthController as ObservedHealthController, bodyPart, -damage, damageInfo);

            // healthController.method_43(bodyPart, damage, damageInfo); // Network

            // Action<EBodyPart, float, DamageInfoStruct> applyDamageEvent = healthController.ApplyDamageEvent;
            // if (applyDamageEvent != null)
            // {
            //     applyDamageEvent(bodyPart, damage, damageInfo);
            // }
            // if (damageInfo.DamageType.IsEnemyDamage())
            // {
            //     Action<Player, IPlayer> onApplyDamageByPlayer = healthController.OnApplyDamageByPlayer;
            //     if (onApplyDamageByPlayer != null)
            //     {
            //         Player player = healthController.Player;
            //         IPlayerOwner player2 = damageInfo.Player;
            //         onApplyDamageByPlayer(player, (player2 != null) ? player2.iPlayer : null);
            //     }
            // }
            // if (!bodyPartState.IsDestroyed && bodyPartState.Health.AtMinimum)
            // {
            //     healthController.DestroyBodyPart(bodyPart, damageType);
            // }
            // if (bodyPartState.IsDestroyed)
            // {
            //     healthController.method_24(bodyPart, damageType);
            // }

            if (!damageType.IsSelfInflicted())
            {
                float num2 = Mathf.Max(0f, damage - num);
                if (num2 > 0f)
                {
                    float num3 = 0f;
                    foreach (KeyValuePair<EBodyPart, GClass3009<NetworkHealthControllerAbstractClass.NetworkBodyEffectsAbstractClass>.BodyPartState> keyValuePair in healthController.Dictionary_0)
                    {
                        EBodyPart ebodyPart;
                        GClass3009<NetworkHealthControllerAbstractClass.NetworkBodyEffectsAbstractClass>.BodyPartState bodyPartState2;
                        keyValuePair.Deconstruct(out ebodyPart, out bodyPartState2);
                        EBodyPart ebodyPart2 = ebodyPart;
                        GClass3009<NetworkHealthControllerAbstractClass.NetworkBodyEffectsAbstractClass>.BodyPartState bodyPartState3 = bodyPartState2;
                        if (ebodyPart2 != bodyPart && !bodyPartState3.IsDestroyed)
                        {
                            num3 += healthController.GetBodyPartHealth(ebodyPart2, false).Maximum;
                        }
                    }
                    float num4 = num2 * Singleton<BackendConfigSettingsClass>.Instance.OverDamageFactor[bodyPart];
                    DamageInfoStruct overDamage = damageInfo.GetOverDamage(bodyPart);
                    foreach (KeyValuePair<EBodyPart, GClass3009<NetworkHealthControllerAbstractClass.NetworkBodyEffectsAbstractClass>.BodyPartState> keyValuePair in healthController.Dictionary_0)
                    {
                        EBodyPart ebodyPart;
                        GClass3009<NetworkHealthControllerAbstractClass.NetworkBodyEffectsAbstractClass>.BodyPartState bodyPartState2;
                        keyValuePair.Deconstruct(out ebodyPart, out bodyPartState2);
                        EBodyPart ebodyPart3 = ebodyPart;
                        GClass3009<NetworkHealthControllerAbstractClass.NetworkBodyEffectsAbstractClass>.BodyPartState bodyPartState4 = bodyPartState2;
                        if (ebodyPart3 != bodyPart && !bodyPartState4.IsDestroyed)
                        {
                            float overDamageReceivedMultiplier = GClass3009<ActiveHealthController.GClass3008>.GClass1728_0.ProfileHealthSettings.BodyPartsSettings[ebodyPart3].OverDamageReceivedMultiplier;
                            // healthController.ChangeHealth(ebodyPart3, Mathf.Min(-num4 * bodyPartState4.Health.Maximum / num3 * overDamageReceivedMultiplier, 0f), overDamage);
                            // if (bodyPartState4.Health.AtMinimum)
                            // {
                            //     healthController.DestroyBodyPart(ebodyPart3, damageType);
                            // }
                        }
                    }
                }

            }

            // ValueStruct bodyPartHealth = healthController.GetBodyPartHealth(EBodyPart.Common, false);
            // D.Dump(bodyPartHealth);
            // if (bodyPartHealth.AtMinimum)
            // {
            //     Singleton<PlayerKilledPacketHandler>.Instance.Send(damageInfo);
            // }


            var headHP = victim.HealthController.GetBodyPartHealth(EBodyPart.Head, false);
            var chestHP = victim.HealthController.GetBodyPartHealth(EBodyPart.Chest, false);

            if (headHP.AtMinimum || chestHP.AtMinimum)
            {
                D.Log($"{victim.Profile.Nickname} died");
                Singleton<PlayerKilledPacketHandler>.Instance.Send(damageInfo, victim.Id);
            }


            float current2 = healthController.GetBodyPartHealth(EBodyPart.Common, false).Current;
            return current - current2;
        }

        public static void ChangeHealth(ObservedHealthController healthController, EBodyPart bodyPart, float value, DamageInfoStruct damageInfo)
        {
            // if (!base.IsAlive)
            // {
            //     return;
            // }
            GClass3009<NetworkHealthControllerAbstractClass.NetworkBodyEffectsAbstractClass>.BodyPartState bodyPartState = healthController.Dictionary_0[bodyPart];
            if (bodyPartState.IsDestroyed)
            {
                return;
            }
            bodyPartState.Health.Current += value;
            float lastDiff = bodyPartState.Health.LastDiff;
            if (lastDiff.IsZero())
            {
                return;
            }
            // healthController.method_36(bodyPart); // network
            // Action<EBodyPart, float, DamageInfoStruct> healthChangedEvent = this.HealthChangedEvent;
            // if (healthChangedEvent != null)
            // {
            //     healthChangedEvent(bodyPart, lastDiff, damageInfo);
            // }
        }
    }
}
