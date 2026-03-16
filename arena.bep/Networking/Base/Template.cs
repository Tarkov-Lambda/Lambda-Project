using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking.Base;
using MemoryPack;

namespace ifp.arena.bep.networking
{
    [MemoryPackable]
    public partial struct TemplatePacket : INetSerializable
    {
        public int id;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<TemplatePacket>(reader);
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

        public override void WhenApproved(TemplatePacket packet, NetPeer peer)
        {
            // H.Notify($"{packet}");
        }
    }
}
