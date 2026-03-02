using Comfort.Common;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.TimeSync;
using UnityEngine;


namespace ifp.arena.bep.networking
{
    public struct ObjectTransformPacket : INetSerializable
    {

        public void Serialize(NetDataWriter writer)
        {

        }

        public void Deserialize(NetDataReader reader)
        {

        }

        public override string ToString()
        {
            return $"";
        }
    }

    public class ObjectTransformPacketHandler : PacketHandler<ObjectTransformPacket>
    {
        public ObjectTransformPacketHandler() : base(DeliveryMethod.Sequenced, PacketAuthority.Both) { }

        public void Send(GameObject gameObject, string objectId)
        {
            var packet = new ObjectTransformPacket
            {

            };

            RequestSend(packet);
        }

        public override void OnReceive(ObjectTransformPacket packet, NetPeer peer)
        {

        }
    }
}