using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main;
using Lambda.Core.Main.Dying;
using MemoryPack;
using System;
using System.Threading;
using UnityEngine;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct PlayerKilledPacket : IPacket, IAuthoredPacket
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
}

// TODO: Rework. The arbitrary 4 second thing bleeds into ragdoll creator and spectator manager without any oversight.
// it's a miracle camera management is somehow functioning.
public class PlayerKilledPacketWarden : LambdaPacketWarden<PlayerKilledPacket>
{
    protected override DeliveryType DeliveryType => DeliveryType.ReliableUnordered;

    public event Action<PlayerKilledPacket> PopKillFeed;

    public void Send(DamageInfoStruct damage, Player victim, Player killer)
    {
        var packet = new PlayerKilledPacket
        {
            Player = victim,
            killer = killer,
            assist = H.GetPlayerContext(victim.Id)?.GetTopAssist(killer),
            damageType = damage.DamageType,
            bodyPartCollider = damage.BodyPartColliderType,
        };

        try
        {
            if (packet.damageType is EDamageType.Bullet or EDamageType.Blunt)
            {
                if (killer != null && killer.HandsController != null && killer.HandsController.Item is not null and Weapon weapon)
                {
                    packet.weaponId = weapon.TemplateId.ToString() ?? string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            D.Log(ex.ToString());
        }

        DispatchPacket(ref packet);
    }

    // protected override bool ValidatePacket(PlayerKilledPacket packet, int peerId, out string rejectionReason)
    // {
    //     if (!packet.Player.Context.IsAlive)
    //     {
    //         rejectionReason = "";
    //         return false;
    //     }

    //     return base.ValidatePacket(packet, peerId, out rejectionReason);
    // }

    // protected override void ApplyOptimistically(PlayerKilledPacket packet)
    // {
    //     HandleKill(packet);
    // }

    protected override void Apply(PlayerKilledPacket packet, int peerId)
    {
        HandleKill(packet);
    }


    // TODO: move logic outta here
    private void HandleKill(PlayerKilledPacket packet)
    {
        PlayerContext victimScore = H.GetPlayerContext(packet.Player);
        // if (!victimScore.IsAlive) return;

        victimScore.Kill();

        PlayerContext killerScore = H.GetPlayerContext(packet.killer);
        if (killerScore != null && killerScore != victimScore && killerScore.Faction != victimScore.Faction)
        {
            killerScore.AddFrag(packet.IsHeadshot);
        }

        if (packet.assist != null)
        {
            PlayerContext assistScore = H.GetPlayerContext(packet.assist);
            if (assistScore != null && assistScore != victimScore && assistScore.Faction != victimScore.Faction)
            {
                assistScore.AddAssist();
            }
        }


        // needs to go elsewhere
        if (H.Gamemode is not IGMRespawnable)
        {
            if (H.Session.matchState is not MatchState.RoundEnd)
            {
                victimScore.SetHardReset();
            }
            else if (H.Session.matchState is MatchState.RoundEnd)
            {
                if (killerScore != victimScore && killerScore.Faction != victimScore.Faction || killerScore == victimScore)
                {
                    victimScore.SetHardReset();
                }
            }
        }

        try
        {
            if (packet.damageType is EDamageType.Bullet or EDamageType.Blunt)
            {
                if (packet.killer != null && packet.killer.HandsController != null && packet.killer.HandsController.Item is not null and Weapon weapon)
                {
                    packet.weaponId = weapon.TemplateId.ToString() ?? string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            D.Log(ex.ToString());
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

        PopKillFeed?.Invoke(packet);
    }

    private async UniTaskVoid HandleLocalPlayerDeath(PlayerKilledPacket packet)
    {
        H.RagdollCreator.CreateLocalPlayerRagdoll();

        HU.HealMe().Forget();

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

    // TODO: THIS NEEDS A CTS
    private async UniTaskVoid HoldPlayerOut(Player victim, Vector3 targetPos, float duration)
    {
        float elapsed = 0f;

        ForcePlayerPosition(victim, targetPos);

        while (elapsed < duration && victim != null && !victim.Destroyed)
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);

            if (victim == null || victim.Destroyed) break;

            var score = H.GetPlayerContext(victim.Id);
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