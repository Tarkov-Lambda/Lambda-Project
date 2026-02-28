using Comfort.Common;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Networking.Base;
using System;
using System.Linq;

namespace ifp.arena.bep.Networking
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

        public override string ToString()
        {
            return $"{id}";
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
            packet.id = netPeer.Id;
            return base.ServerValidation(ref packet, netPeer);
        }

        public override void OnReceive(TemplatePacket packet)
        {
            
        }
    }
}