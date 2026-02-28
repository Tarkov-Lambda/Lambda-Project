using Comfort.Common;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Networking.Base;
using UnityEngine;


namespace ifp.arena.bep.Networking
{
    public struct ObjectTransformPacket : INetSerializable
    {
        public int id;
        public int objectId;

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
            objectId = reader.GetInt();
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

        public void Send(GameObject gameObject, int objectId)
        {
            var packet = new ObjectTransformPacket
            {
                objectId = objectId,
                position = gameObject.transform.position,
                rotation = gameObject.transform.rotation,
            };

            RequestSend(packet);
        }

        // Server validation ensures the packet ID matches the sender (Anti-spoof)
        public override bool ServerValidation(ref ObjectTransformPacket packet, NetPeer netPeer)
        {
            packet.id = netPeer.Id;
            return base.ServerValidation(ref packet, netPeer);
        }

        public override void OnReceive(ObjectTransformPacket packet)
        {
            if (NetworkedPhysicsObject.Registry.TryGetValue(packet.objectId, out var netObject) && packet.id != Singleton<IFikaNetworkManager>.Instance.NetId)
            {
                netObject.UpdateNetworkState(packet.position, packet.rotation);
            }
        }
    }
}