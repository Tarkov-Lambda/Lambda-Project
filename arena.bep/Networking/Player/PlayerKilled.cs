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
        if (killer == null)
        {
            killer = H.GetPlayer(damage.Player.iPlayer.Id);
        }

        if (victim == null && !H.IsHeadless)
        {
            victim = H.MainPlayer;
        }


        var packet = new PlayerKilledPacket
        {
            killer = killer,
            victim = victim,
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

        if (packet.weaponId == null)
        {
            packet.weaponId = "safasdf";
        }

        D.Log("Server was here");

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

    private void HandleKill(PlayerKilledPacket packet)
    {

        D.Log("asdsadasda");
        if (packet.weaponId == "safasdf")
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

        if (!H.IsHeadless)
        {
            if (packet.victim.IsYourPlayer)
            {
                HU.HealMe().Forget();
                Singleton<ReplenishPacketHandler>.Instance.Send();

                packet.victim.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
                Singleton<RagdollCreator>.Instance.CreateLocalPlayerRagdoll();

                _ = PU.CloseEyes(true, true);

                H.MainPlayer.SetEmptyHands(delegate { });
            }
            else
            {
                Singleton<RagdollCreator>.Instance.OnPacket(packet.victim);
            }
        }


        Teleporter.Teleport(packet.victim, "lobby", Faction.None);
        EventBus.OnPlayerKill.Invoke(packet);
    }
}