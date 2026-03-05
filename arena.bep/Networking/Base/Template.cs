using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking.Base;

namespace ifp.arena.bep.networking
{
    public struct TemplatePacket : INetSerializable
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

    public class TemplatePacketHandler : PacketHandler<TemplatePacket>
    {
        public void Send(int id)
        {
            var packet = new TemplatePacket
            {
                id = id,
            };

            RequestSend(packet);
        }

        public override bool ServerValidation(ref TemplatePacket packet, NetPeer netPeer)
        {
            return base.ServerValidation(ref packet, netPeer);
        }

        public override void OnReceive(TemplatePacket packet, NetPeer peer)
        {
            // H.Notify($"{packet}");
        }
    }
}