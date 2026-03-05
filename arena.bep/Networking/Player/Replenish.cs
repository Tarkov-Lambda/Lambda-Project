using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;

namespace ifp.arena.bep.networking
{
    public struct ReplenishPacket : INetSerializable
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

        public override string ToString()
        {
            return $"{id}";
        }
    }

    public class ReplenishPacketHandler : PacketHandler<ReplenishPacket>
    {
        public void Send()
        {
            var packet = new ReplenishPacket
            {
                id = H.MainPlayer.Id,
            };

            RequestSend(packet);
        }

        public override void OnReceive(ReplenishPacket packet, NetPeer peer)
        {
            PlayerUtils.Replenish(H.GetPlayer(packet.id));
        }
    }
}