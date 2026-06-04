using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using Fika.Core.Main.Components;
using Fika.Core.Main.GameMode;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Packets.Backend;
using Fika.Core.Networking.Packets.Player.Common;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using HarmonyLib;
using Lambda.Core.Networking;
using PacketWarden.TimeSync;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace Lambda.Core.Patches;

internal class Patch_FikaServer_OnCommonPlayerPacketReceived : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaServer), "OnCommonPlayerPacketReceived");

    [PatchPrefix]
    private static bool Prefix(FikaServer __instance, CoopHandler ____coopHandler, CommonPlayerPacket packet, NetPeer peer)
    {
        if (packet.Type != ECommonSubPacketType.Damage) return true;
        if (packet.SubPacket is not DamagePacket damage) return true;

        int victimNetId = packet.NetId;

        if (!____coopHandler.Players.TryGetValue(victimNetId, out FikaPlayer victim)) return true;

        Player shooter = H.GetPlayer(damage.ProfileId);
        PlayerContext shooterScore = shooter.GetContext();

        if (!shooterScore.IsAlive)
        {
            if (NetworkTime.ServerNowSeconds - shooterScore.DeathTimestamp > 0.03)
                return false;
        }

        return true;
    }

    [PatchPostfix]
    private static void Postfix(FikaServer __instance, CoopHandler ____coopHandler, CommonPlayerPacket packet, NetPeer peer)
    {
        if (packet.Type != ECommonSubPacketType.Damage) return;
        if (packet.SubPacket is not DamagePacket damage) return;

        int victimNetId = packet.NetId;

        if (!____coopHandler.Players.TryGetValue(victimNetId, out FikaPlayer victim)) return;

        Player shooter = H.GetPlayer(damage.ProfileId);

        victim.GetContext()?.RecordDamageTaken(shooter, damage.Damage);

        // we handle the server owner player natively through ActiveHealthController
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
            ArmorDamage = damage.ArmorDamage,
        };

        // Instead of waiting for healthsync from the client, we apply a damage packet directly on the server on a player that's not ours.
        // to see if the damage is lethal and broadcast death before the client does
        Predict_ApplyDamage(victim, damage.BodyPartType, damage.Damage, damageInfo, damage);

        shooter.GetContext().AddDamage((int)Math.Round(damageInfo.Damage));
    }

    public static float Predict_ApplyDamage(FikaPlayer victim, EBodyPart bodyPart, float damage, DamageInfoStruct damageInfo, DamagePacket damagePacket)
    {
        if (!H.GetPlayerScore(victim.Id).IsAlive) return 0f;

        ObservedHealthController healthController = victim.HealthController as ObservedHealthController;

        if (!H.IsHeadless)
        {
            if (H.MainPlayer.ActiveHealthController.DamageMultiplier > 1f || bodyPart != EBodyPart.Head || !damageInfo.DamageType.IsEnemyDamage())
            {
                damage *= H.MainPlayer.ActiveHealthController.DamageMultiplier;
            }
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

                float num4 = num2 * H.BackendConfigSettingsClass.OverDamageFactor[bodyPart];
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

        if (headHP.AtMinimum || chestHP.AtMinimum || bodyPartHealth.AtMinimum)
        {
            Player shooter = H.GetPlayer(damagePacket.ProfileId);

            Singleton<PlayerKilledPacketWarden>.Instance.Send(damageInfo, victim, shooter); // Client dies
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



// FRAGILE (complete function overwrite)
internal class Patch_FikaServer_OnNetworkReceiveUnconnected : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaServer), nameof(FikaServer.OnNetworkReceiveUnconnected));

    [PatchPrefix]
    private static bool Prefix(FikaServer __instance, CoopHandler ____coopHandler, NetManager ____netServer, IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        bool flag = false;
        if (____coopHandler != null && ____coopHandler.LocalGameInstance != null && H.IFikaGame.GameController.RaidStarted)
        {
            flag = true;
        }

        string result;
        if (messageType == UnconnectedMessageType.Broadcast)
        {
            D.Log("[SERVER] Received discovery request. Send discovery response");
            NetDataWriter netDataWriter = new NetDataWriter();
            netDataWriter.Put(1);
            ____netServer.SendUnconnectedMessage(netDataWriter.AsReadOnlySpan(), remoteEndPoint);
        }
        else if (reader.TryGetString(out result))
        {
            NetDataWriter netDataWriter2 = new NetDataWriter();
            string text = FikaBackendUtils.ServerGuid.ToString();
            if (result == text)
            {
                bool flag2 = reader.GetBool();
                // netDataWriter2.Put((flag && !flag2) ? "fika.inprogress" : "fika.hello");
                netDataWriter2.Put("fika.hello");
                ____netServer.SendUnconnectedMessage(netDataWriter2.AsReadOnlySpan(), remoteEndPoint);
            }
            else
            {
                D.LogError("PingingRequest::Data was not as expected: " + result);
                netDataWriter2.Put("fika.reject");
                ____netServer.SendUnconnectedMessage(netDataWriter2.AsReadOnlySpan(), remoteEndPoint);
            }
        }
        else
        {
            D.LogError("PingingRequest: Could not parse string");
        }

        return false;
    }
}

// FRAGILE (complete function overwrite)
internal class Patch_FikaServer_OnConnectionRequest : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaServer), nameof(FikaServer.OnConnectionRequest));

    [PatchPrefix]
    private static bool Prefix(CoopHandler ____coopHandler, NetManager ____netServer, NetDataWriter ____dataWriter, ConnectionRequest request)
    {
        request.Accept();
        return false;


        if (____coopHandler != null && ____coopHandler.LocalGameInstance != null && H.IFikaGame.GameController.RaidStarted)
        {
            if (request.Data.GetString() == "fika.reconnect")
            {
                request.Accept();
                return false;
            }
            ____dataWriter.Reset();
            ____dataWriter.Put("Raid already started");
            request.Reject(____dataWriter);

            return false;
        }

        request.AcceptIfKey("fika.core");

        return false;
    }
}

internal class Patch_FikaServer_StopNatIntroduceRoutine : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaServer), nameof(FikaServer.StopNatIntroduceRoutine));

    [PatchPrefix]
    private static bool Prefix(FikaServer __instance)
    {
        return false;
    }
}

internal class Patch_FikaServer_OnDestroy : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaServer), "OnDestroy");

    [PatchPrefix]
    private static bool Prefix(FikaServer __instance, CancellationTokenSource ____cts)
    {
        if (____cts != null)
        {
            ____cts.Cancel();
        }

        return true;
    }
}

// we do not have any loot items in the mod ig?
// for some reason it errors
public class Patch_HostGameController_GetHostLootItems : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(HostGameController), nameof(HostGameController.GetHostLootItems));

    [PatchPrefix]
    private static bool Prefix(ref byte[] __result)
    {
        __result = GetEmptyLootData();
        return false;
    }

    private static byte[] _emptyLootData;

    public static byte[] GetEmptyLootData()
    {
        if (_emptyLootData != null) return _emptyLootData;

        var emptyLootList = new List<LootItemPositionClass>();

        var target = EFTItemSerializerClass.SerializeLootData(emptyLootList, FikaGlobals.SearchControllerSerializer);
        var writer = WriterPoolManager.GetWriter();

        GClass3695.WriteEFTLootDataDescriptor(writer, target);
        _emptyLootData = writer.ToArray();

        WriterPoolManager.ReturnWriter(writer);

        return _emptyLootData;
    }
}

// either because of what I did or the new 2.2.4 patch
// during disconnect - the observed player's snapshot timestamps are not reset/cleared (or vice versa the client does not get the timestamp that they left off)
// because of that the server will reject every single movement packet from the reconnected client
// causing the player to be motionless until their reconnected timestamp state reaches the old timestamp
// as a result we are checking if the observed player already exists OnNetworkSettingsPacketReceived
// and clearing the snapshotter manually 
public class Patch_FikaServer_OnNetworkSettingsPacketReceived : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaServer), "OnNetworkSettingsPacketReceived");

    [PatchPrefix]
    static void Prefix(FikaServer __instance, NetworkSettingsPacket packet)
    {
        var coopHandler = __instance.CoopHandler;
        if (coopHandler == null) return;

        foreach (var player in coopHandler.Players.Values)
        {
            if (player.ProfileId == packet.ProfileId && player is ObservedPlayer observedPlayer)
            {
                observedPlayer.ResetSnapshotter();
                return;
            }
        }
    }
}