using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using Fika.Core.Main.Components;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using HarmonyLib;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System.Linq;
using System.Reflection;
using UnityEngine;

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
            if (packet.Type is ECommonSubPacketType.HealthSync)
            {
                HealthSyncPacket subPacket = packet.SubPacket as HealthSyncPacket;
                if (subPacket.Packet.SyncType is NetworkHealthSyncPacketStruct.ESyncType.BodyHealth)
                {
                    D.Log(packet.NetId.ToString());
                    D.Log(packet.Type.GetType().ToString());
                    D.Dump(packet.SubPacket);
                }

            }

            if (packet.Type != ECommonSubPacketType.Damage) return;
            if (packet.SubPacket is not DamagePacket damage) return;

            var coopHandler = CoopHandlerRef(__instance);
            int victimNetId = packet.NetId;

            if (!coopHandler.Players.TryGetValue(victimNetId, out var victim)) return;

            // D.Log(peer.Id.ToString());
            // D.Log(damage.ProfileId);
            var damagePlayer = H.AllPlayers.FirstOrDefault(p => p.ProfileId == damage.ProfileId);

            // D.Log(damagePlayer.Profile.Nickname);
            // D.Log(damagePlayer.Id.ToString());

            // we handle the server owner player natively
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
            Predict_ApplyDamage(victim, damage.BodyPartType, damage.Damage, damageInfo);

            victim.HandleDamagePacket(damage);
        }

        // This is fucking retarded but the alternative is to create activehealthcontroller for each player and that's even more retarded
        // Intended to be a lightweight damage simulation so that killing still feels quite responsive
        // Hopefully this does not cause too much desync server side (at the end of the day we are still fully healing the player after death)
        public static float Predict_ApplyDamage(FikaPlayer victim, EBodyPart bodyPart, float damage, DamageInfoStruct damageInfo)
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
            var bodyPartState = healthController.Dictionary_0[bodyPart];

            float num = bodyPartState.Health.Current;
            float current = healthController.GetBodyPartHealth(EBodyPart.Common, false).Current;

            ChangeHealth(healthController, bodyPart, -damage, damageInfo);

            // healthController.method_43(bodyPart, damage, damageInfo);

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

                    foreach (var kvp in healthController.Dictionary_0)
                    {
                        var part = kvp.Key;
                        var state = kvp.Value;

                        if (part != bodyPart && !state.IsDestroyed)
                        {
                            num3 += healthController.GetBodyPartHealth(part, false).Maximum;
                        }
                    }

                    float num4 = num2 * Singleton<BackendConfigSettingsClass>.Instance.OverDamageFactor[bodyPart];
                    DamageInfoStruct overDamage = damageInfo.GetOverDamage(bodyPart);

                    foreach (var kvp in healthController.Dictionary_0)
                    {
                        var part = kvp.Key;
                        var state = kvp.Value;

                        if (part != bodyPart && !state.IsDestroyed)
                        {
                            float mult = GClass3009<ActiveHealthController.GClass3008>.GClass1728_0.ProfileHealthSettings.BodyPartsSettings[part].OverDamageReceivedMultiplier;

                            ChangeHealth(healthController, part, Mathf.Min(-num4 * state.Health.Maximum / num3 * mult, 0f), overDamage);

                            // if (state.Health.AtMinimum)
                            // {
                            //     healthController.DestroyBodyPart(part, damageType);
                            // }
                        }
                    }
                }

                // if (damage >= 1f && damageType != EDamageType.Barbed)
                // {
                //     healthController.method_27(bodyPart, 0f, 0f,
                //         Mathf.Clamp(15f * damage / bodyPartState.Health.Maximum, 1f, 10f),
                //         new float?(Mathf.Clamp(damage / bodyPartState.Health.Maximum * 2f, 0.33f, 2f)));
                // }

                // if (damageType == EDamageType.Btr)
                // {
                //     healthController.DoStun(12f, 1f);
                // }
            }

            ValueStruct bodyPartHealth = healthController.GetBodyPartHealth(EBodyPart.Common, false);

            var headHP = victim.HealthController.GetBodyPartHealth(EBodyPart.Head, false);
            var chestHP = victim.HealthController.GetBodyPartHealth(EBodyPart.Chest, false);

            D.Dump(headHP);
            D.Dump(chestHP);
            if (headHP.AtMinimum || chestHP.AtMinimum || bodyPartHealth.AtMinimum)
            {
                Singleton<PlayerKilledPacketHandler>.Instance.Send(damageInfo, victim.Id); // Client dies
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
