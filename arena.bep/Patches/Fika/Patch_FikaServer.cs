using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.Interactive;
using Fika.Core.Main.Components;
using Fika.Core.Main.GameMode;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Packets;
using Fika.Core.Networking.Packets.Backend;
using Fika.Core.Networking.Packets.Generic;
using Fika.Core.Networking.Packets.Generic.SubPackets;
using Fika.Core.Networking.Packets.Player.Common;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using Fika.Core.Networking.Packets.World;
using Fika.Core.Networking.Pooling;
using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep.Patches;

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

        Player shooter = H.AllPlayers.FirstOrDefault(p => p.ProfileId == damage.ProfileId);
        PlayerScore shooterScore = shooter.GetScore();

        if (!shooterScore.IsAlive)
        {
            if (NetworkTime.LocalNowSeconds - shooterScore.DeathTimestamp > 0.05)
                return false;
        }

        return true;
    }

    [PatchPostfix]
    private static void Postfix(FikaServer __instance, CoopHandler ____coopHandler, CommonPlayerPacket packet, NetPeer peer)
    {
        // D.Log($"{peer.Id} sent {packet.GetType()} {packet.Type}");
        // if (packet.Type is ECommonSubPacketType.HealthSync)
        // {
        // HealthSyncPacket subPacket = packet.SubPacket as HealthSyncPacket;
        // if (subPacket.Packet.SyncType is NetworkHealthSyncPacketStruct.ESyncType.BodyHealth)
        // {
        //     D.Log(packet.NetId.ToString());
        //     D.Log(packet.Type.GetType().ToString());
        //     D.Dump(packet.SubPacket);
        // }
        // }

        if (packet.Type != ECommonSubPacketType.Damage) return;
        if (packet.SubPacket is not DamagePacket damage) return;

        int victimNetId = packet.NetId;

        if (!____coopHandler.Players.TryGetValue(victimNetId, out FikaPlayer victim)) return;

        // D.Log(peer.Id.ToString());
        // D.Log(damage.ProfileId);
        Player shooter = H.AllPlayers.FirstOrDefault(p => p.ProfileId == damage.ProfileId);

        // D.Log(shooter.Profile.Nickname);
        // D.Log(damagePlayer.Id.ToString());

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

        // Instead of waiting for healthsync, we apply a damage packet directly on the server on a player that's not ours.
        // I can't vouch as per how accurate this is going to be
        // but in theory this should be just fine, and if the client heals, they will send a healthsync packet later
        Predict_ApplyDamage(victim, damage.BodyPartType, damage.Damage, damageInfo, damage);

        // victim.HandleDamagePacket(damage); // is this even supposed to be here? I'm in postfix lol

        H.GetPlayerScore(shooter).AddDamage((int)Math.Round(damageInfo.Damage));
    }

    // This is fucking retarded but the alternative is to create activehealthcontroller for each player and that's even more retarded
    // Intended to be a lightweight damage simulation so that killing still feels quite responsive
    // Hopefully this does not cause too much desync server side (at the end of the day we are still fully healing the player after death)
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
            Player shooter = H.AllPlayers.FirstOrDefault(p => p.ProfileId == damagePacket.ProfileId);

            Singleton<PlayerKilledPacketHandler>.Instance.Send(damageInfo, victim, shooter); // Client dies
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

// we do not have any loot items in the game mod
public class Patch_HostGameController_GetHostLootItems : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(HostGameController), nameof(HostGameController.GetHostLootItems));
    }

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


public class Debug_FikaHeadless_Handshake : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(FikaServer), "OnLoadingProfilePacketReceived");
    }

    [PatchPostfix]
    static void Postfix(FikaServer __instance, LoadingProfilePacket packet, NetPeer peer)
    {
        var profile = packet.Profiles.Keys.FirstOrDefault();
        D.Log($"[DEBUG] Handshake Postfix: {profile?.Nickname} connected to Headless.");

        // Let's see if the server thinks it should send this new guy to everyone else
        // and if it should send everyone else to this new guy.
        var coopHandler = Traverse.Create(__instance).Field("_coopHandler").GetValue<Fika.Core.Main.Components.CoopHandler>();
        if (coopHandler != null)
        {
            foreach (var existing in coopHandler.HumanPlayers)
            {
                D.Log($"[DEBUG] Server currently tracking existing player: {existing.Profile.Nickname} (NetID: {existing.NetId})");
            }
        }
    }
}

public class Debug_FikaReconnect_NRE_Hunter : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(FikaServer), "OnReconnectPacketReceived");
    }

    [PatchPrefix]
    static bool Prefix(FikaServer __instance, CoopHandler ____coopHandler, ReconnectPacket packet, NetPeer peer)
    {
        D.Log("[FikaReconnect-Debug] Entered OnReconnectPacketReceived");

        try
        {
            if (__instance == null) { D.LogError("[FikaReconnect-Debug] __instance is null!"); return false; }
            if (packet == null) { D.LogError("[FikaReconnect-Debug] packet is null!"); return false; }
            if (peer == null) { D.LogError("[FikaReconnect-Debug] peer is null!"); return false; }

            if (!packet.IsRequest)
            {
                D.Log("[FikaReconnect-Debug] packet.IsRequest is false, exiting.");
                return false;
            }

            if (packet.InitialRequest)
            {
                D.Log("[FikaReconnect-Debug] Processing InitialRequest...");
                NotificationManagerClass.DisplayMessageNotification(
                    LocaleUtils.RECONNECT_REQUESTED.Localized(),
                    iconType: EFT.Communications.ENotificationIconType.Alert);

                if (____coopHandler == null) { D.LogError("[FikaReconnect-Debug] ____coopHandler is null!"); return false; }
                if (____coopHandler.HumanPlayers == null) { D.LogError("[FikaReconnect-Debug] ____coopHandler.HumanPlayers is null!"); return false; }

                foreach (var player in ____coopHandler.HumanPlayers)
                {
                    if (player == null) { D.LogError("[FikaReconnect-Debug] A HumanPlayer in loop is null!"); continue; }

                    if (player.ProfileId == packet.ProfileId && player is ObservedPlayer observedPlayer)
                    {
                        D.Log($"[FikaReconnect-Debug] Found matching ObservedPlayer: {player.ProfileId}");

                        if (observedPlayer.Profile == null) D.LogError("[FikaReconnect-Debug] observedPlayer.Profile is null!");
                        if (observedPlayer.NetworkHealthController == null) D.LogError("[FikaReconnect-Debug] observedPlayer.NetworkHealthController is null!");

                        ReconnectPacket ownCharacterPacket = new()
                        {
                            Type = ReconnectPacket.EReconnectDataType.OwnCharacter,
                            Profile = observedPlayer.Profile,
                            ProfileHealthClass = observedPlayer.NetworkHealthController?.Store(),
                            PlayerPosition = observedPlayer.Position
                        };

                        D.Log("[FikaReconnect-Debug] Sending OwnCharacter ReconnectPacket");
                        CallSendDataToPeer(__instance, ownCharacterPacket, DeliveryMethod.ReliableOrdered, peer);

                        if (observedPlayer.HealthBar == null) D.LogError("[FikaReconnect-Debug] observedPlayer.HealthBar is null!");
                        else observedPlayer.HealthBar.ClearEffects();

                        D.Log("[FikaReconnect-Debug] Sending ClearEffects generic packet");

                        try
                        {
                            __instance.SendGenericPacket(EGenericSubPacketType.ClearEffects, ClearEffects.FromValue(observedPlayer.NetId), true, peer);
                        }
                        catch (Exception ex)
                        {
                            D.LogError($"[FikaReconnect-Debug] Failed to invoke SendGenericPacket: {ex.Message}");
                        }
                    }
                }

                D.Log("[FikaReconnect-Debug] Finished InitialRequest");
                return false;
            }

            D.Log("[FikaReconnect-Debug] Processing Main Reconnect Data...");

            var gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null) { D.LogError("[FikaReconnect-Debug] Singleton<GameWorld>.Instance is null!"); return false; }
            if (gameWorld.World_0 == null) { D.LogError("[FikaReconnect-Debug] gameWorld.World_0 is null!"); return false; }

            var worldTraverse = Traverse.Create(gameWorld.World_0);

            if (gameWorld.Grenades == null) D.LogError("[FikaReconnect-Debug] gameWorld.Grenades is null!");
            else
            {
                D.Log("[FikaReconnect-Debug] Processing Grenades");
                var grenades = gameWorld.Grenades.GetValuesEnumerator();
                List<SmokeGrenadeDataPacketStruct> smokeData = new();
                foreach (var item in grenades)
                {
                    if (item == null) { D.LogError("[FikaReconnect-Debug] Grenade item is null!"); continue; }
                    if (item is SmokeGrenade smokeGrenade)
                    {
                        smokeData.Add(smokeGrenade.NetworkData);
                    }
                }

                if (smokeData.Count > 0)
                {
                    D.Log($"[FikaReconnect-Debug] Sending {smokeData.Count} SmokeGrenade data");
                    ReconnectPacket throwablePacket = new() { Type = ReconnectPacket.EReconnectDataType.Throwable, ThrowableData = smokeData };
                    CallSendDataToPeer(__instance, throwablePacket, DeliveryMethod.ReliableOrdered, peer);
                }
            }

            D.Log("[FikaReconnect-Debug] Processing InteractiveObjects");
            var interactivesField = worldTraverse.Field<WorldInteractiveObject[]>("worldInteractiveObject_0");
            if (interactivesField == null) D.LogError("[FikaReconnect-Debug] worldInteractiveObject_0 field not found via Reflection!");
            else if (interactivesField.Value == null) D.LogError("[FikaReconnect-Debug] worldInteractiveObject_0 array is null!");
            else
            {
                List<WorldInteractiveObject.WorldInteractiveDataPacketStruct> interactivesData = new();
                foreach (var interactiveObject in interactivesField.Value)
                {
                    if (interactiveObject == null) { D.LogError("[FikaReconnect-Debug] Interactive object in array is null!"); continue; }
                    if ((interactiveObject.DoorState != interactiveObject.InitialDoorState && interactiveObject.DoorState != EDoorState.Interacting) ||
                        (interactiveObject is Door door && door.IsBroken))
                    {
                        interactivesData.Add(interactiveObject.GetStatusInfo(true));
                    }
                }

                if (interactivesData.Count > 0)
                {
                    D.Log($"[FikaReconnect-Debug] Sending {interactivesData.Count} Interactives data");
                    ReconnectPacket interactivePacket = new() { Type = ReconnectPacket.EReconnectDataType.Interactives, InteractivesData = interactivesData };
                    CallSendDataToPeer(__instance, interactivePacket, DeliveryMethod.ReliableOrdered, peer);
                }
            }

            D.Log("[FikaReconnect-Debug] Processing LampControllers");
            var lampControllers = LocationScene.GetAllObjects<LampController>(false);
            if (lampControllers == null) D.LogError("[FikaReconnect-Debug] LocationScene.GetAllObjects<LampController> returned null!");
            else
            {
                Dictionary<int, byte> lampStates = new();
                foreach (var controller in lampControllers)
                {
                    if (controller == null) { D.LogError("[FikaReconnect-Debug] LampController in array is null!"); continue; }
                    lampStates.Add(controller.NetId, (byte)controller.LampState);
                }

                if (lampStates.Count > 0)
                {
                    D.Log($"[FikaReconnect-Debug] Sending {lampStates.Count} Lamp states");
                    ReconnectPacket lampPacket = new() { Type = ReconnectPacket.EReconnectDataType.LampControllers, LampStates = lampStates };
                    CallSendDataToPeer(__instance, lampPacket, DeliveryMethod.ReliableOrdered, peer);
                }
            }

            D.Log("[FikaReconnect-Debug] Processing Windows");
            if (gameWorld.Windows == null) D.LogError("[FikaReconnect-Debug] gameWorld.Windows is null!");
            else
            {
                var windows = gameWorld.Windows.GetValuesEnumerator();
                Dictionary<int, Vector3> windowData = new();
                foreach (var window in windows)
                {
                    if (window == null) { D.LogError("[FikaReconnect-Debug] window item is null!"); continue; }
                    if (window.AvailableToSync && window.IsDamaged)
                    {
                        // BUG CATCH: Calling .Value on a nullable that has no value behaves exactly like an NRE
                        if (!window.FirstHitPosition.HasValue)
                        {
                            D.LogError($"[FikaReconnect-Debug] Window {window.NetId} is damaged but FirstHitPosition is null!");
                        }
                        else
                        {
                            windowData.Add(window.NetId, window.FirstHitPosition.Value);
                        }
                    }
                }

                if (windowData.Count > 0)
                {
                    D.Log($"[FikaReconnect-Debug] Sending {windowData.Count} Windows data");
                    ReconnectPacket windowPacket = new() { Type = ReconnectPacket.EReconnectDataType.Windows, WindowBreakerStates = windowData };
                    CallSendDataToPeer(__instance, windowPacket, DeliveryMethod.ReliableOrdered, peer);
                }
            }

            D.Log("[FikaReconnect-Debug] Processing Other Players");
            if (____coopHandler == null) D.LogError("[FikaReconnect-Debug] ____coopHandler is null (checking players)!");
            else if (____coopHandler.Players == null) D.LogError("[FikaReconnect-Debug] ____coopHandler.Players is null!");
            else
            {
                foreach (var player in ____coopHandler.Players.Values)
                {
                    if (player == null) { D.LogError("[FikaReconnect-Debug] Player in Players.Values is null!"); continue; }
                    if (player.ProfileId == packet.ProfileId) continue;

                    D.Log($"[FikaReconnect-Debug] Processing Player {player.ProfileId}");
                    if (player.Profile == null) D.LogError($"[FikaReconnect-Debug] Player {player.ProfileId} Profile is null!");
                    if (player.InventoryController == null) D.LogError($"[FikaReconnect-Debug] Player {player.ProfileId} InventoryController is null!");
                    if (player.HealthController == null) D.LogError($"[FikaReconnect-Debug] Player {player.ProfileId} HealthController is null!");

                    var characterPacket = SendCharacterPacket.FromValue(new()
                    {
                        Profile = player.Profile,
                        ControllerId = player.InventoryController?.CurrentId ?? "",
                        FirstOperationId = player.InventoryController?.NextOperationId ?? 0
                    },
                    player.HealthController?.IsAlive ?? false, player.IsAI, player.Position, player.NetId);

                    if (player.ActiveHealthController != null)
                    {
                        characterPacket.PlayerInfoPacket.HealthByteArray = player.ActiveHealthController.SerializeState();
                    }
                    else if (player is ObservedPlayer observedPlayer2)
                    {
                        if (observedPlayer2.NetworkHealthController == null) D.LogError($"[FikaReconnect-Debug] observedPlayer {player.ProfileId} NetworkHealthController is null!");
                        else characterPacket.PlayerInfoPacket.HealthByteArray = observedPlayer2.NetworkHealthController.Store().SerializeHealthInfo();
                    }

                    if (player.HandsController != null)
                    {
                        if (player.HandsController.Item == null) D.LogError($"[FikaReconnect-Debug] Player {player.ProfileId} HandsController.Item is null!");
                        else
                        {
                            characterPacket.PlayerInfoPacket.ControllerType = HandsControllerToEnumClass.FromController(player.HandsController);
                            characterPacket.PlayerInfoPacket.ItemId = player.HandsController.Item.Id;
                            characterPacket.PlayerInfoPacket.IsStationary = player.MovementContext?.IsStationaryWeaponInHands ?? false;
                        }
                    }

                    try
                    {
                        __instance.SendGenericPacketToPeer(EGenericSubPacketType.SendCharacter, characterPacket, peer);
                    }
                    catch (Exception ex)
                    {
                        D.LogError($"[FikaReconnect-Debug] Failed to invoke SendGenericPacket: {ex.Message}");
                    }
                }
            }

            D.Log("[FikaReconnect-Debug] Processing Stashes (BTR/Transit)");
            StashesPacket stashesPacket = new();
            if (gameWorld.BtrController != null)
            {
                D.Log("[FikaReconnect-Debug] Found BtrController");
                stashesPacket.HasBTR = true;
                if (gameWorld.BtrController.TransferItemsController == null) D.LogError("[FikaReconnect-Debug] BtrController.TransferItemsController is null!");
                else if (gameWorld.BtrController.TransferItemsController.List_0 == null) D.LogError("[FikaReconnect-Debug] TransferItemsController.List_0 is null!");
                else
                {
                    var length = gameWorld.BtrController.TransferItemsController.List_0.Count;
                    stashesPacket.BTRStashes = new StashItemClass[length];
                    for (var i = 0; i < length; i++)
                    {
                        stashesPacket.BTRStashes[i] = gameWorld.BtrController.TransferItemsController.List_0[i];
                    }
                }
            }

            if (gameWorld.TransitController != null)
            {
                D.Log("[FikaReconnect-Debug] Found TransitController");
                stashesPacket.HasTransit = true;
                if (gameWorld.TransitController.TransferItemsController == null) D.LogError("[FikaReconnect-Debug] TransitController.TransferItemsController is null!");
                else if (gameWorld.TransitController.TransferItemsController.List_0 == null) D.LogError("[FikaReconnect-Debug] TransitController.TransferItemsController.List_0 is null!");
                else
                {
                    var length = gameWorld.TransitController.TransferItemsController.List_0.Count;
                    stashesPacket.TransitStashes = new StashItemClass[length];
                    for (var i = 0; i < length; i++)
                    {
                        stashesPacket.TransitStashes[i] = gameWorld.TransitController.TransferItemsController.List_0[i];
                    }
                }
            }

            D.Log("[FikaReconnect-Debug] Sending Stashes packet");
            CallSendDataToPeer(__instance, stashesPacket, DeliveryMethod.ReliableOrdered, peer);

            D.Log("[FikaReconnect-Debug] Sending Finished packet");
            ReconnectPacket finishPacket = new() { Type = ReconnectPacket.EReconnectDataType.Finished };
            CallSendDataToPeer(__instance, finishPacket, DeliveryMethod.ReliableOrdered, peer);

            D.Log("[FikaReconnect-Debug] Reconnect successfully processed to completion.");
        }
        catch (Exception ex)
        {
            D.LogError($"[FikaReconnect-Debug] FATAL EXCEPTION CAUGHT: {ex.Message}\n{ex.StackTrace}");
        }

        // Must return false to entirely skip the original crashy function!
        return false;
    }

    // --- Reflection Helpers to replace private function calls ---

    private static void CallSendDataToPeer<T>(FikaServer instance, T packet, DeliveryMethod deliveryMethod, NetPeer peer)
    {
        try
        {
            var method = AccessTools.GetDeclaredMethods(typeof(FikaServer))
                .FirstOrDefault(m => m.Name == "SendDataToPeer" && m.IsGenericMethod);

            if (method != null)
            {
                var genericMethod = method.MakeGenericMethod(typeof(T));
                object[] args = new object[] { packet, deliveryMethod, peer };
                genericMethod.Invoke(instance, args);
            }
            else
            {
                D.LogError("[FikaReconnect-Debug] CallSendDataToPeer: Method not found!");
            }
        }
        catch (Exception ex)
        {
            D.LogError($"[FikaReconnect-Debug] Failed to invoke SendDataToPeer: {ex.Message}");
        }
    }

    private static void CallSendGenericPacket(FikaServer instance, EGenericSubPacketType type, ISubPacket subPacket, bool isReliable, NetPeer peer)
    {
        try
        {
            Traverse.Create(instance).Method("SendGenericPacket", type, subPacket, isReliable, peer).GetValue();
        }
        catch (Exception ex)
        {
            D.LogError($"[FikaReconnect-Debug] Failed to invoke SendGenericPacket: {ex.Message}");
        }
    }

    private static void CallSendGenericPacketToPeer(FikaServer instance, EGenericSubPacketType type, ISubPacket subPacket, NetPeer peer)
    {
        try
        {
            Traverse.Create(instance).Method("SendGenericPacketToPeer", type, subPacket, peer).GetValue();
        }
        catch (Exception ex)
        {
            D.LogError($"[FikaReconnect-Debug] Failed to invoke SendGenericPacketToPeer: {ex.Message}");
        }
    }
}