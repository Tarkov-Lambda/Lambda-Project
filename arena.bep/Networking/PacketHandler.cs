using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Patches;
using System;
using System.Linq;
using System.Net.Sockets;

namespace ifp.arena.bep.Networking
{
    public abstract class PacketHandler<T> : IDisposable where T : INetSerializable, new()
    {
        public PacketHandler()
        {
            Patch_Gameworld_OnGameStarted.OnGameStarted += RegisterPacket;

            // Hot-Reload
            if (Singleton<GameWorld>.Instance != null)
            {
                RegisterPacket(Singleton<GameWorld>.Instance);
            }
        }

        public void RegisterPacket(GameWorld gameWorld)
        {
            Singleton<IFikaNetworkManager>.Instance.RegisterPacket<T>(BroadcastAndReceive);
        }

        public void Dispose() {
            if (Singleton<IFikaNetworkManager>.Instance != null)
            {
                NetPacketProcessor netPacketProcessor = AccessTools.Field(typeof(FikaServer), "_packetProcessor").GetValue(Singleton<IFikaNetworkManager>.Instance) as NetPacketProcessor;
                netPacketProcessor.RemoveSubscription<T>();
            }
        }

        public void Send(T packet)
        {
            Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, DeliveryMethod.ReliableOrdered, FikaBackendUtils.IsServer);

            if (FikaBackendUtils.IsServer)
            {
                BroadcastAndReceive(packet);
            }
        }

        private void BroadcastAndReceive(T packet)
        {
            if (FikaBackendUtils.IsServer)
            {
                Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, DeliveryMethod.ReliableOrdered, true);
            }

            OnReceive(packet);
        }

        public abstract void OnReceive(T packet);
    }
}
