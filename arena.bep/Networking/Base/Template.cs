using Comfort.Common;
using EFT;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Networking.Base;
using System;
using System.Linq;

namespace ifp.arena.bep.Networking
{
    public struct TemplatePacket : INetSerializable
    {
        public int templateInt;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(templateInt);
        }

        public void Deserialize(NetDataReader reader)
        {
            templateInt = reader.GetInt();
        }

        public override string ToString()
        {
            return $"{templateInt}";
        }
    }

    public class TemplatePacketHandler : PacketHandler<TemplatePacket>
    {
        public void Send(int templateInt)
        {
            var packet = new TemplatePacket
            {
                templateInt = templateInt,
            };

            RequestSend(packet);
        }

        public override void OnReceive(TemplatePacket packet)
        {
            
        }
    }
}