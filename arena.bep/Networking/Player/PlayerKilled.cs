using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core.Dying;
using PacketHandler;
using MemoryPack;
using System;
using UnityEngine;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PlayerKilledPacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; } // Player is the victim

    [MemoryPackAllowSerialize]
    public Player killer;

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
            return bodyPartCollider switch
            {
                EBodyPartColliderType.HeadCommon
                or EBodyPartColliderType.BackHead
                or EBodyPartColliderType.Jaw
                or EBodyPartColliderType.Eyes
                or EBodyPartColliderType.Ears
                or EBodyPartColliderType.ParietalHead => true,
                _ => false,
            };
        }
    }

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<PlayerKilledPacket>(reader);
}

public class PlayerKilledPacketHandler : PacketHandler<PlayerKilledPacket>
{
    public void Send(DamageInfoStruct damage, Player victim, Player killer)
    {
        // D.Log(victim.Profile.Nickname);
        // D.Log(killer.Profile.Nickname);
        // D.Dump(damage);

        var packet = new PlayerKilledPacket
        {
            Player = victim,
            killer = killer,
            assist = null,
            damageType = damage.DamageType,
            bodyPartCollider = damage.BodyPartColliderType,
        };

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

    protected override void Apply(PlayerKilledPacket packet, NetPeer peer)
    {
        HandleKill(packet);
    }


    // this logic needs to be abstracted elsewhere
    private void HandleKill(PlayerKilledPacket packet)
    {
        PlayerScore victimScore = H.GetPlayerScore(packet.Player);
        if (!victimScore.IsAlive) return;

        victimScore.Kill();

        PlayerScore killerScore = H.GetPlayerScore(packet.killer);
        if (killerScore != null && killerScore != victimScore && killerScore.Faction != victimScore.Faction)
        {
            killerScore.AddFrag(packet.IsHeadshot);
            if (H.Session.matchState is MatchState.RoundAction or MatchState.RoundPlanted)
            {
                victimScore.SetHardReset();
            }
        }

        if (packet.weaponId == null)
        {
            try
            {
                packet.weaponId = killerScore.player?.HandsController?.Item?.TemplateId ?? "";
            }
            catch (Exception ex)
            {
                D.Log(ex.ToString());
            }
        }

        // if (H.IsHeadless)
        // {
        //     Teleporter.Teleport(packet.victim, "lobby", Faction.None);
        //     return;
        // }

        if (packet.Player.IsYourPlayer)
        {
            HandleLocalPlayerDeath(packet).Forget();
        }
        else
        {
            H.RagdollCreator.CreateRagdollFromPlayer(packet.Player);

            // teleport without interpolation
            HoldPlayerOut(packet.Player, Vector3.zero, 2.0f).Forget();

            Teleporter.Teleport(packet.Player, "lobby", Faction.None);
        }
    }

    private async UniTaskVoid HandleLocalPlayerDeath(PlayerKilledPacket packet)
    {
        H.RagdollCreator.CreateLocalPlayerRagdoll();

        // Do local cleanup
        HU.HealMe().Forget();
        // Singleton<ReplenishPacketHandler>.Instance.Send();
        packet.Player.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
        _ = PU.CloseEyes(true, true);
        H.MainPlayer.SetEmptyHands(delegate { });

        // essentially teleport without interpolation
        Vector3 deathPos = packet.Player.Position;
        Vector3 hiddenPos = deathPos + new Vector3(0, -10f, 0);
        HoldPlayerOut(packet.Player, hiddenPos, 4.0f).Forget();

        // wait for the death cam sequence to finish in RagdollCreator
        await UniTask.Delay(4000, ignoreTimeScale: false, PlayerLoopTiming.Update);
        H.MainPlayer.MovementContext.ResetFlying();

        // if we are already alive after 4 seconds, do not teleport ourselves into the lobby
        if (!H.MainPlayerScore.IsAlive)
        {
            Teleporter.Teleport(packet.Player, "lobby", Faction.None);
        }
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

            // If the player has been respawned (e.g. RoundPrepare fired mid-death sequence),
            // stop fighting the respawn teleport and release control immediately.
            var score = H.GetPlayerScore(victim.Id);
            if (score != null && score.IsAlive) break;

            ForcePlayerPosition(victim, targetPos);
            elapsed += Time.deltaTime;
        }

        // re-enable player controller once the hold is over
        if (victim != null && !victim.Destroyed && victim._characterController != null)
        {
            victim._characterController.isEnabled = true;
        }

        victim.MovementContext.ResetFlying();
    }

    private void ForcePlayerPosition(Player victim, Vector3 pos)
    {
        // disable the character controller for a bit
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