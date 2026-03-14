using System;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
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

        public override string ToString() => $"{killerId} killed {victimId}";
    }

    public class PlayerKilledPacketHandler : PacketHandler<PlayerKilledPacket>
    {
        public void Send(DamageInfoStruct damage)
        {
            int killerId = damage.Player != null ? damage.Player.iPlayer.Id : 1;

            var packet = new PlayerKilledPacket
            {
                killerId = killerId,
                victimId = H.MainPlayer.Id,
                assistId = 12312345, // idk how to make this yet tbh
                damageType = damage.DamageType,
                bodyPartCollider = damage.BodyPartColliderType,
                weaponId = H.GetPlayer(killerId).HandsController.Item.Id,
            };

            RequestSend(packet);
        }

        public override void WhenApproved(PlayerKilledPacket packet, NetPeer peer)
        {
            if (H.Scoreboard[packet.killerId] != null)
            {
                H.Scoreboard[packet.killerId].AddFrag(packet.IsHeadshot);
            }

            if (H.Scoreboard[packet.victimId] != null)
            {
                H.Scoreboard[packet.victimId].Kill();
            }

            FakeTeleport(packet);

            EventBus.OnPlayerKill(packet);
        }

        private void FakeTeleport(PlayerKilledPacket packet)
        {
            UniTask.Delay(25).ContinueWith(() =>
            {
                Player victim = H.GetPlayer(packet.victimId);
                if (victim != null && victim != H.MainPlayer)
                {
                    H.GetPlayer(packet.victimId).Position = new UnityEngine.Vector3();
                }
            });
        }
    }
}
