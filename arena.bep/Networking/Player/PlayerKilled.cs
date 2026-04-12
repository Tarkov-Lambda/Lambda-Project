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
    public void Send(DamageInfoStruct damage, Player victim = null)
    {
        int killerId = damage.Player != null ? damage.Player.iPlayer.Id : 1;

        var killer = H.GetPlayer(killerId);

        var packet = new PlayerKilledPacket
        {
            killer = killer,
            victim = H.MainPlayer,
            assist = null,
            damageType = damage.DamageType,
            bodyPartCollider = damage.BodyPartColliderType,
            weaponId = H.GetPlayer(killerId).HandsController.Item.TemplateId,
        };

        if (victim != null) packet.victim = victim;

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
        PlayerScore victimScore = H.GetPlayerScore(packet.victim);
        if (!victimScore.IsAlive) return;

        PlayerScore killerScore = H.GetPlayerScore(packet.killer);

        victimScore.Kill();

        if (killerScore != victimScore && killerScore.Faction != victimScore.Faction)
        {
            killerScore.AddFrag(packet.IsHeadshot);
        }

        // create corpse before anything else happens
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

        Teleporter.Teleport(packet.victim, "lobby", Faction.None);
        EventBus.OnPlayerKill.Invoke(packet);
    }
}