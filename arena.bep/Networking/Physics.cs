using Comfort.Common;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking.Base;
using UnityEngine;


namespace ifp.arena.bep.networking
{
    public struct ObjectTransformPacket : INetSerializable
    {
        public int id;
        public string objectId;

        public Vector3 position;
        public Quaternion rotation;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(id);
            writer.Put(objectId);
            writer.Put(position);
            writer.Put(rotation);
        }

        public void Deserialize(NetDataReader reader)
        {
            id = reader.GetInt();
            objectId = reader.GetString();
            position = reader.GetVector3();
            rotation = reader.GetQuaternion();
        }

        public override string ToString()
        {
            return $"{id} | Pos: {position} | Rot: {rotation}";
        }
    }

    public class ObjectTransformPacketHandler : PacketHandler<ObjectTransformPacket>
    {
        public ObjectTransformPacketHandler() : base(DeliveryMethod.Sequenced, PacketAuthority.Both)
        {
            Instance = this;
        }

        public void Send(GameObject gameObject, string objectId)
        {
            var packet = new ObjectTransformPacket
            {
                objectId = objectId,
                position = gameObject.transform.position,
                rotation = gameObject.transform.rotation,
            };

            RequestSend(packet);
        }

        public override bool ServerValidation(ref ObjectTransformPacket packet, NetPeer peer)
        {
            return base.ServerValidation(ref packet, peer);
        }

        public override void OnReceive(ObjectTransformPacket packet, NetPeer peer)
        {
            if (NetworkedPhysicsObject.Registry.TryGetValue(packet.objectId, out var netObject) && packet.id != Singleton<IFikaNetworkManager>.Instance.NetId)
            {
                netObject.UpdateNetworkState(packet.position, packet.rotation);
            }
        }
    }
}