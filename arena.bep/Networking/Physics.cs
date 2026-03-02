using Comfort.Common;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.TimeSync;
using System;
using UnityEngine;

namespace ifp.arena.bep.networking
{
    /// <summary>
    /// Server -> Client physics snapshots.
    /// 
    /// Authoritative simulation is on the host/server. Clients interpolate.
    /// </summary>
    public struct PhysicsSnapshotPacket : INetSerializable
    {
        public struct State
        {
            public uint netId;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 angularVelocity;
            public byte flags;
        }

        public double serverTimeSeconds;
        public ushort serverTick;
        public byte count;
        public State[] states;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(serverTimeSeconds);
            writer.Put(serverTick);
            writer.Put(count);

            if (count <= 0 || states == null)
                return;

            for (int i = 0; i < count; i++)
            {
                var s = states[i];
                writer.Put(s.netId);
                writer.Put(s.position);
                writer.Put(s.rotation);
                writer.Put(s.velocity);
                writer.Put(s.angularVelocity);
                writer.Put(s.flags);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            serverTimeSeconds = reader.GetDouble();
            serverTick = reader.GetUShort();
            count = reader.GetByte();

            if (count == 0)
            {
                states = Array.Empty<State>();
                return;
            }

            states = new State[count];
            for (int i = 0; i < count; i++)
            {
                states[i] = new State
                {
                    netId = reader.GetUInt(),
                    position = reader.GetVector3(),
                    rotation = reader.GetQuaternion(),
                    velocity = reader.GetVector3(),
                    angularVelocity = reader.GetVector3(),
                    flags = reader.GetByte(),
                };
            }
        }

        public override string ToString()
        {
            return $"t={serverTimeSeconds:F3} tick={serverTick} count={count}";
        }
    }

    public class PhysicsSnapshotPacketHandler : PacketHandler<PhysicsSnapshotPacket>
    {
        public PhysicsSnapshotPacketHandler() : base(DeliveryMethod.Sequenced, PacketAuthority.ServerOnly) { }

        public void SendSnapshot(PhysicsSnapshotPacket packet)
        {
            if (!FikaBackendUtils.IsServer)
                return;

            RequestSend(packet);
        }

        public override void OnReceive(PhysicsSnapshotPacket packet, NetPeer peer)
        {
            // Server already simulated; it doesn't need to apply received snapshots.
            if (FikaBackendUtils.IsServer)
                return;

            // Bootstrap clock quickly from authoritative stamp if we don't have sync yet.
            NetworkTime.BootstrapFromServerStamp(packet.serverTimeSeconds);

            if (packet.count == 0 || packet.states == null)
                return;

            for (int i = 0; i < packet.count; i++)
            {
                var st = packet.states[i];
                if (NetworkedPhysicsObject.TryGet(st.netId, out var obj))
                {
                    obj.PushSnapshot(packet.serverTimeSeconds, st.position, st.rotation, st.velocity, st.angularVelocity, st.flags);
                }
            }
        }
    }
}