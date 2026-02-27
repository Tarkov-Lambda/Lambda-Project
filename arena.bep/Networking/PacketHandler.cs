using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using FlyingWormConsole3.LiteNetLib;
using HarmonyLib;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Patches;
using System;
using DeliveryMethod = Fika.Core.Networking.LiteNetLib.DeliveryMethod;

namespace ifp.arena.bep.Networking
{
    public enum PacketAuthority
    {
        Both,       // Anyone can send/receive
        ServerOnly  // Only Server can create/send. Clients only receive.
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

            if (Singleton<GameWorld>.Instance != null)
            {
                RegisterPacket(Singleton<GameWorld>.Instance);
            }
        }

        public void RegisterPacket(GameWorld gameWorld)
        {
            Singleton<IFikaNetworkManager>.Instance.RegisterPacket<T>(BroadcastAndReceive);
        }

        public void Dispose()
        {
            Release(this);

            if (Singleton<IFikaNetworkManager>.Instance != null)
            {
                NetPacketProcessor netPacketProcessor = AccessTools.Field(typeof(FikaServer), "_packetProcessor").GetValue(Singleton<IFikaNetworkManager>.Instance) as NetPacketProcessor;
                netPacketProcessor?.RemoveSubscription<T>();
            }
        }

        protected void RequestSend(T packet)
        {
            if (authority == PacketAuthority.ServerOnly && !FikaBackendUtils.IsServer)
            {
                return;
            }

            Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, deliveryMethod, FikaBackendUtils.IsServer);

            if (FikaBackendUtils.IsServer)
            {
                OnReceive(packet);
            }
        }

        private void BroadcastAndReceive(T packet)
        {
            if (FikaBackendUtils.IsServer)
            {
                if (authority == PacketAuthority.ServerOnly)
                {
                    Plugin.Logger.LogInfo("Unauthorized Packet");
                    return;
                }

                // Does two things at once and I'm not sure if I like the way it works
                // It validates and optionally modifies the packet, and then returns a bool to decide whether the packet is valid
                // note that this function only runs when the server has received a packet from a client
                bool validPacket = ServerValidation(ref packet);
                if (!validPacket) return;

                Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, deliveryMethod, true);
            }

            OnReceive(packet);
        }

        public virtual bool ServerValidation(ref T packet)
        {
            return true;
        }

        public abstract void OnReceive(T packet);
    }
}