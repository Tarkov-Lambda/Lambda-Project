using System;
using System.Linq;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking
{
    [MemoryPackable]
    public partial struct PlayerKilledPacket : INetSerializable
    {
        public int killerId;
        public int victimId;
        public int assistId;
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

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<PlayerKilledPacket>(reader);
    }

    public class PlayerKilledPacketHandler : PacketHandler<PlayerKilledPacket>
    {
        public void Send(DamageInfoStruct damage, int? victimId = null)
        {
            int killerId = damage.Player != null ? damage.Player.iPlayer.Id : 1;

            var packet = new PlayerKilledPacket
            {
                killerId = killerId,
                victimId = H.MainPlayer.Id,
                assistId = 12312345, // idk how to make this yet tbh
                damageType = damage.DamageType,
                bodyPartCollider = damage.BodyPartColliderType,
                weaponId = H.GetPlayer(killerId).HandsController.Item.TemplateId,
            };

            if (victimId.HasValue) packet.victimId = victimId.Value;

            RequestSend(packet);
        }

        public void Send(DamagePacket damage)
        {
            int victimId = damage.NetId;

            int killerId = 0;
            string weaponTemplateId = "";

            if (damage.ProfileId.HasValue)
            {
                var killerPlayer = H.AllPlayers.FirstOrDefault(p => p.ProfileId == damage.ProfileId.Value.ToString());
                if (killerPlayer != null)
                {
                    killerId = killerPlayer.Id;
                    if (killerPlayer.HandsController?.Item != null)
                        weaponTemplateId = killerPlayer.HandsController.Item.TemplateId;
                }
            }

            var packet = new PlayerKilledPacket
            {
                killerId = killerId,
                victimId = victimId,
                assistId = 12312345,
                damageType = damage.DamageType,
                bodyPartCollider = damage.ColliderType,
                weaponId = weaponTemplateId,
            };

            RequestSend(packet);
        }

        protected override void LocalPredictApproved(PlayerKilledPacket packet)
        {

        }

        protected override void WhenApproved(PlayerKilledPacket packet, NetPeer peer)
        {
            PlayerScore killerScore = H.GetPlayerScore(packet.killerId);
            PlayerScore victimScore = H.GetPlayerScore(packet.victimId);

            victimScore.Kill();

            if (killerScore != victimScore && killerScore.faction != victimScore.faction)
            {
                killerScore.AddFrag(packet.IsHeadshot);
            }

            // create corpse before anything else happens
            Die(victimScore.player);
        }

        private void Die(Player player)
        {
            if (player.IsYourPlayer)
            {

                HU.HealMe().Forget();
                Singleton<ReplenishPacketHandler>.Instance.Send();

                player.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
                Singleton<RagdollCreator>.Instance.CreateLocalPlayerRagdoll();

                _ = PU.CloseEyes(true, true);

                H.MainPlayer.SetEmptyHands(delegate { });
            }
            else
            {
                Singleton<RagdollCreator>.Instance.OnPacket(player);
            }

            player.Teleport(player.Position + new UnityEngine.Vector3(0f, -10f, 0f));

            Teleporter.Teleport(player, "lobby", Faction.None);
        }
    }
}
