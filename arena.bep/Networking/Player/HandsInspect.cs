using Comfort.Common;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using System.Linq;
using static EFT.Player;

namespace ifp.arena.bep.networking
{
    public struct HandsInspectPacket : INetSerializable
    {
        public int id;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(id);
        }

        public void Deserialize(NetDataReader reader)
        {
            id = reader.GetInt();
        }
    }

    public class HandsInspectPacketHandler : PacketHandler<HandsInspectPacket>
    {
        public void Send(int id)
        {
            var packet = new HandsInspectPacket
            {
                id = id
            };
            RequestSend(packet);
        }

        public override void WhenApproved(HandsInspectPacket packet, NetPeer peer)
        {
            var player = H.GetPlayer(packet.id);

            if (player.IsYourPlayer) return;

            if (player.HandsController is EmptyHandsController emptyHands)
            {
                emptyHands.ExamineWeapon();
            }
        }
    }
}