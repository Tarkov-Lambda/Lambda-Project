using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using MemoryPack;
using static EFT.Player;

namespace ifp.arena.bep.networking
{
    [MemoryPackable]
    public partial struct HandsInspectPacket : INetSerializable
    {
        public int id;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);

        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<HandsInspectPacket>(reader);
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
