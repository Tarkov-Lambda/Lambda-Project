using Comfort.Common;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;

namespace ifp.arena.bep.networking
{
    public struct BlindFirePacket : INetSerializable
    {
        public int id;
        public int value; // -1 = side fire, 0 = none, 1 = over-top

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(id);
            writer.Put(value);
        }

        public void Deserialize(NetDataReader reader)
        {
            id = reader.GetInt();
            value = reader.GetInt();
        }
    }

    public class BlindFirePacketHandler : PacketHandler<BlindFirePacket>
    {
        public void Send(int id, int value)
        {
            var packet = new BlindFirePacket
            {
                id = id,
                value = value
            };
            RequestSend(packet);
        }

        public override void WhenApproved(BlindFirePacket packet, NetPeer peer)
        {
            var player = H.GetPlayer(packet.id);
            if (player == null || player.IsYourPlayer) return;

            player.HandsController?.BlindFire(packet.value);
        }
    }
}
