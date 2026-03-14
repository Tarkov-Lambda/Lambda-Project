using System;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;

namespace ifp.arena.bep.networking
{
    public struct PlayerKilledPacket : INetSerializable
    {
        public int killerId;
        public int victimId;
        public int assistId;
        public bool isHeadshot;
        public string weaponId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(killerId);
            writer.Put(victimId);
            writer.Put(assistId);
            writer.Put(isHeadshot);
            writer.Put(weaponId);
        }

        public void Deserialize(NetDataReader reader)
        {
            killerId = reader.GetInt();
            victimId = reader.GetInt();
            assistId = reader.GetInt();
            isHeadshot = reader.GetBool();
            weaponId = reader.GetString();
        }

        public override string ToString()
        {
            return $"{killerId} killed {victimId}";
        }
    }

    public class PlayerKilledPacketHandler : PacketHandler<PlayerKilledPacket>
    {
        public void Send(DamageInfoStruct damage)
        {
            int killerId = damage.Player != null ? damage.Player.iPlayer.Id : 1;
            bool isHeadshot = damage.BodyPartColliderType
            is EBodyPartColliderType.HeadCommon
            or EBodyPartColliderType.BackHead
            or EBodyPartColliderType.Jaw
            or EBodyPartColliderType.Eyes
            or EBodyPartColliderType.Ears
            or EBodyPartColliderType.ParietalHead;

            var packet = new PlayerKilledPacket
            {
                killerId = killerId,
                victimId = H.MainPlayer.Id,
                assistId = 12312345, // idk how to make this yet tbh
                isHeadshot = isHeadshot,
                weaponId = H.GetPlayer(killerId).HandsController.Item.Id,
            };

            RequestSend(packet);
        }

        public override void WhenApproved(PlayerKilledPacket packet, NetPeer peer)
        {
            if (H.Scoreboard[packet.killerId] != null)
            {
                H.Scoreboard[packet.killerId].AddFrag(false);
            }

            if (H.Scoreboard[packet.victimId] != null)
            {
                H.Scoreboard[packet.victimId].Kill();
            }

            // Plugin.Logger.LogInfo($"main player died");

            // Whenever player teleports
            // Player interpolation simply causes them to fly away
            // We have to slightly delay, and then brute force change player position
            FakeTeleport(packet);

            // H.Notify(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
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