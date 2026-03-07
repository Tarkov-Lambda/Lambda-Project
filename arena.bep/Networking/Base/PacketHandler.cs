using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.Core;
using System;

namespace ifp.arena.bep.networking.Base
{
    public enum PacketAuthority
    {
        Both,       // Anyone can send/receive
        ServerOnly  // Only Server can send. Clients only receive.
    }

    public struct RejectedPacket<T> : INetSerializable where T : INetSerializable, new()
    {
        public T Payload;

        public void Serialize(NetDataWriter writer)
        {
            Payload.Serialize(writer);
        }

        public void Deserialize(NetDataReader reader)
        {
            Payload = new T();
            Payload.Deserialize(reader);
        }
    }

    public abstract class PacketHandler<T> : Singleton<PacketHandler<T>>, IDisposable where T : INetSerializable, new()
    {
        protected DeliveryMethod deliveryMethod;
        protected PacketAuthority authority;

        public PacketHandler(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered, PacketAuthority authority = PacketAuthority.Both)
        {
            this.deliveryMethod = deliveryMethod;
            this.authority = authority;

            H.OnGameStarted += RegisterPacket;
            H.OnGameDispose += UnregisterPacket;

            // Hot-reload
            RegisterPacket(H.GameWorld);
        }

        private NetPacketProcessor GetPacketProcessor()
        {
            var manager = H.FikaNet;
            if (manager == null) return null;

            var field = AccessTools.Field(manager.GetType(), "_packetProcessor");

            return field?.GetValue(manager) as NetPacketProcessor;
        }

        public void RegisterPacket(GameWorld gameWorld)
        {
            if (H.isInRaid())
            {
                Plugin.Logger.LogInfo($"Registering {typeof(T).Name}");
                if (FikaBackendUtils.IsServer)
                {
                    H.FikaNet.RegisterPacket<T, NetPeer>(WhenServerReceivesPacket);
                    H.FikaNet.RegisterPacket<RejectedPacket<T>, NetPeer>((packet, peer) => { });
                }
                else
                {
                    H.FikaNet.RegisterPacket<T, NetPeer>(WhenClientReceivesPacket);
                    H.FikaNet.RegisterPacket<RejectedPacket<T>, NetPeer>(WhenClientReceivesRejection);
                }
            }
        }

        public void UnregisterPacket(GameWorld gameWorld)
        {
            Plugin.Logger.LogInfo($"Disposing {typeof(T).FullName}");

            try
            {
                var processor = GetPacketProcessor();
                if (processor == null) return;

                processor.RemoveSubscription<T>();
                processor.RemoveSubscription<RejectedPacket<T>>();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Safe dispose failed: {ex}");
            }
        }

        public void Dispose()
        {
            UnregisterPacket(null);
            Release(this);
        }

        protected void RequestSend(T packet)
        {
            if (H.Arena != null && H.GameWorld is HideoutGameWorld) return;

            if (authority == PacketAuthority.ServerOnly && FikaBackendUtils.IsClient)
                return;

            H.FikaNet.SendData(ref packet, deliveryMethod, FikaBackendUtils.IsServer);

            if (FikaBackendUtils.IsServer)
            {
                WhenApproved(packet, Singleton<NetPeer>.Instance);
            }
            else
            {
                ClientPrediction(packet);
            }
        }

        private void WhenServerReceivesPacket(T packet, NetPeer netPeer)
        {
            if (authority == PacketAuthority.ServerOnly)
            {
                Plugin.Logger.LogInfo("Unauthorized Packet");
                return;
            }

            bool validPacket = ServerValidation(ref packet, netPeer);
            if (!validPacket)
            {
                var processor = GetPacketProcessor();
                if (processor != null)
                {
                    var rejected = new RejectedPacket<T> { Payload = packet };
                    H.FikaNet.SendDataToPeer(ref rejected, deliveryMethod, netPeer);
                }
                return;
            }

            if (ShouldBroadcastClientPacket(packet))
            {
                H.FikaNet.SendData(ref packet, deliveryMethod, true);
            }

            WhenApproved(packet, netPeer);
        }

        private void WhenClientReceivesPacket(T packet, NetPeer netPeer)
        {
            WhenApproved(packet, netPeer);
        }

        private void WhenClientReceivesRejection(RejectedPacket<T> rejectedPacket, NetPeer netPeer)
        {
            WhenRejected(rejectedPacket.Payload, netPeer);
        }

        protected virtual bool ShouldBroadcastClientPacket(T packet) => true;

        public virtual bool ServerValidation(ref T packet, NetPeer netPeer)
        {
            return true;
        }

        public virtual void ClientPrediction(T packet) { }

        public abstract void WhenApproved(T packet, NetPeer netPeer);

        // This will now successfully trigger when the server rejects it!
        public virtual void WhenRejected(T packet, NetPeer netPeer) { }
    }
}