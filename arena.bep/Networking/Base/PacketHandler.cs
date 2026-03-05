using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.Patches.Tarkov;
using System;
using DeliveryMethod = Fika.Core.Networking.LiteNetLib.DeliveryMethod;

namespace ifp.arena.bep.networking.Base
{
    public enum PacketAuthority
    {
        Both,       // Anyone can send/receive
        ServerOnly  // Only Server can send. Clients only receive.
    }

    public abstract class PacketHandler<T> : Singleton<PacketHandler<T>>, IDisposable where T : INetSerializable, new()
    {
        protected DeliveryMethod deliveryMethod;
        protected PacketAuthority authority;

        public PacketHandler(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered, PacketAuthority authority = PacketAuthority.Both)
        {
            this.deliveryMethod = deliveryMethod;
            this.authority = authority;

            Patch_Gameworld_OnGameStarted.OnGameStarted += RegisterPacket;

            if (H.GameWorld != null && H.GameWorld is not HideoutGameWorld)
            {
                RegisterPacket(H.GameWorld);
            }
        }

        public void RegisterPacket(GameWorld gameWorld)
        {
            Plugin.Logger.LogInfo($"Registering {typeof(T).Name}");
            if (FikaBackendUtils.IsServer)
            {
                H.FikaNet.RegisterPacket<T, NetPeer>(BroadcastAndReceive);
            }
            else
            {
                H.FikaNet.RegisterPacket<T, NetPeer>(OnReceive);
            }
        }

        public void Dispose()
        {
            Plugin.Logger.LogInfo($"Disposing {typeof(T).FullName}");

            Patch_Gameworld_OnGameStarted.OnGameStarted -= RegisterPacket;

            try
            {
                var manager = H.FikaNet;

                if (manager == null || manager.Equals(null))
                    return;

                var field = AccessTools.Field(manager.GetType(), "_packetProcessor");
                if (field == null)
                    return;

                var processor = field.GetValue(manager) as NetPacketProcessor;
                if (processor == null)
                    return;

                processor.RemoveSubscription<T>();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Safe dispose failed: {ex}");
            }

            Release(this);
        }

        // EXPLANATION:
        // Server Send -> Server RequestSend -> Server Broadcast Packet -> Server/All Clients OnReceive (Server at no ping, All Clients at ping)
        // Local Client Send -> Local Client RequestSend -> Server BroadcastAndReceive -> Server ServerValidation -> Server Broadcast Packet -> Server/All Clients OnReceive

        protected void RequestSend(T packet)
        {
            if (H.Arena != null && H.GameWorld is HideoutGameWorld) return;

            // Save traffic
            if (authority == PacketAuthority.ServerOnly && FikaBackendUtils.IsClient)
            {
                return;
            }

            H.FikaNet.SendData(ref packet, deliveryMethod, FikaBackendUtils.IsServer);

            if (FikaBackendUtils.IsServer)
            {
                OnReceive(packet, Singleton<NetPeer>.Instance);
            }
        }

        private void BroadcastAndReceive(T packet, NetPeer netPeer)
        {
            if (authority == PacketAuthority.ServerOnly)
            {
                Plugin.Logger.LogInfo("Unauthorized Packet");
                return;
            }

            // Does two things at once and I'm not sure if I like the way it works
            // It validates and optionally modifies the packet, and then returns a bool to decide whether the packet is valid
            // note that this function only runs when the server has received a packet from a client
            bool validPacket = ServerValidation(ref packet, netPeer);
            if (!validPacket) return;

            // Most client->server packets are broadcast back out to all clients.
            // time sync packets between client and server need it, so
            if (ShouldBroadcastClientPacket(packet))
            {
                H.FikaNet.SendData(ref packet, deliveryMethod, true);
            }

            // We might want to add artificial lag here if the server is not headless.
            OnReceive(packet, netPeer);
        }

        // Override to prevent the server from re-broadcasting a client->server packet to other clients.
        // Default behavior matches existing implementation (broadcast everything).
        protected virtual bool ShouldBroadcastClientPacket(T packet) => true;

        public virtual bool ServerValidation(ref T packet, NetPeer netPeer)
        {
            return true;
        }

        // Server applies this via BroadcastAndReceive (instantly)
        // Local client receives its own packet at ping time
        public abstract void OnReceive(T packet, NetPeer netPeer);
    }
}