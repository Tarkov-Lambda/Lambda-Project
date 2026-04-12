using System.Linq;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using PacketHandler;
using ifp.arena.shared;
using MemoryPack;
using System;
using ifp.arena.bep.Patches.Tarkov;
using UnityEngine;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PlayerKilledPacket : INetSerializable
{
    [MemoryPackAllowSerialize]
    public Player killer;

    [MemoryPackAllowSerialize]
    public Player victim;

    [MemoryPackAllowSerialize]
    public Player assist;

    public EDamageType damageType;
    public EBodyPartColliderType bodyPartCollider;
    public string weaponId;

    [MemoryPackIgnore]
    public bool IsHeadshot
    {
        get
        {
            switch (bodyPartCollider)
            {
                case EBodyPartColliderType.HeadCommon:
                case EBodyPartColliderType.BackHead:
                case EBodyPartColliderType.Jaw:
                case EBodyPartColliderType.Eyes:
                case EBodyPartColliderType.Ears:
                case EBodyPartColliderType.ParietalHead:
                    return true;
                default:
                    return false;
            }
        }
    }

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<PlayerKilledPacket>(reader);
}

public class PlayerKilledPacketHandler : PacketHandler<PlayerKilledPacket>
{
    public void Send(DamageInfoStruct damage, Player victim = null, Player killer = null)
    {
        var packet = new PlayerKilledPacket
        {
            killer = killer,
            victim = victim,
            assist = null,
            damageType = damage.DamageType,
            bodyPartCollider = damage.BodyPartColliderType,
        };

        if (killer == null && damage.Player?.iPlayer != null)
        {
            packet.killer = H.GetPlayer(damage.Player.iPlayer.Id);
        }

        if (victim == null && !H.IsHeadless)
        {
            packet.killer = H.MainPlayer;
        }

        try
        {
            packet.weaponId = killer?.HandsController?.Item?.TemplateId ?? "";
        }
        catch (Exception ex)
        {
            D.Log(ex.ToString());
        }

        DispatchPacket(packet);
    }

    protected override void LocalPredictApproved(PlayerKilledPacket packet)
    {
        HandleKill(packet);
    }

    protected override void WhenApproved(PlayerKilledPacket packet, NetPeer peer)
    {
        HandleKill(packet);
    }


    // this logic needs to be abstracted elsewhere
    private void HandleKill(PlayerKilledPacket packet)
    {
        if (packet.weaponId == "")
        {
            packet.weaponId = packet.killer?.HandsController?.Item?.TemplateId;
        }

        PlayerScore victimScore = H.GetPlayerScore(packet.victim);
        if (!victimScore.IsAlive) return;

        PlayerScore killerScore = H.GetPlayerScore(packet.killer);
        victimScore.Kill();

        if (killerScore != victimScore && killerScore.Faction != victimScore.Faction)
        {
            killerScore.AddFrag(packet.IsHeadshot);
        }

        EventBus.OnPlayerKill.Invoke(packet);

        if (H.IsHeadless)
        {
            Teleporter.Teleport(packet.victim, "lobby", Faction.None);
            return;
        }

        if (packet.victim.IsYourPlayer)
        {
            HandleLocalPlayerDeath(packet).Forget();
        }
        else
        {
            Singleton<RagdollCreator>.Instance.OnPacket(packet.victim);

            // 2. Banish them 500 meters underground instantly to hide network latency
            Vector3 shadowRealmPos = packet.victim.Position + new Vector3(0, -500f, 0);
            HoldPlayerOut(packet.victim, shadowRealmPos, 2.0f).Forget();

            Teleporter.Teleport(packet.victim, "lobby", Faction.None);
        }
    }

    private async UniTaskVoid HandleLocalPlayerDeath(PlayerKilledPacket packet)
    {
        Singleton<RagdollCreator>.Instance.CreateLocalPlayerRagdoll();

        // 2. Do local cleanup
        HU.HealMe().Forget();
        Singleton<ReplenishPacketHandler>.Instance.Send();
        packet.victim.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
        _ = PU.CloseEyes(true, true);
        H.MainPlayer.SetEmptyHands(delegate { });

        // 3. Hide the real player body so it doesn't stand inside the death camera!
        // We drop them just 3 meters down. We CANNOT move them to the lobby yet, 
        // otherwise EFT's occlusion culling will unload the map around the death cam!
        Vector3 deathPos = packet.victim.Position;
        Vector3 hiddenPos = deathPos + new Vector3(0, -3f, 0);
        HoldPlayerOut(packet.victim, hiddenPos, 4.0f).Forget();

        // 4. Wait for the death cam sequence to finish (RagdollCreator uses 4000ms)
        await UniTask.Delay(4000, ignoreTimeScale: false, PlayerLoopTiming.Update);

        // 5. NOW teleport them to the lobby safely!
        Teleporter.Teleport(packet.victim, "lobby", Faction.None);
    }

    private async UniTaskVoid HoldPlayerOut(Player victim, Vector3 targetPos, float duration)
    {
        float elapsed = 0f;

        // Force position immediately before loop starts
        ForcePlayerPosition(victim, targetPos);

        while (elapsed < duration && victim != null && !victim.Destroyed)
        {
            // PostLateUpdate ensures we override AFTER EFT's networking and IK calculates
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);

            if (victim == null || victim.Destroyed) break;

            ForcePlayerPosition(victim, targetPos);
            elapsed += Time.deltaTime;
        }

        // Safely re-enable their controller once the hold duration is over
        if (victim != null && !victim.Destroyed && victim._characterController != null)
        {
            victim._characterController.isEnabled = true;
        }
    }

    private void ForcePlayerPosition(Player victim, Vector3 pos)
    {
        // Disable controller so gravity doesn't make them fall endlessly
        if (victim._characterController != null)
            victim._characterController.isEnabled = false;

        victim.gameObject.transform.position = pos;
        victim.Transform.position = pos;
        victim.Position = pos;

        if (victim.PlayerBones?.BodyTransform != null)
            victim.PlayerBones.BodyTransform.position = pos;

        if (victim.MovementContext != null)
            victim.MovementContext.TransformPosition = pos;
    }
}