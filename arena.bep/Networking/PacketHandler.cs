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
using System.Net.Sockets;
using DeliveryMethod = Fika.Core.Networking.LiteNetLib.DeliveryMethod;

namespace ifp.arena.bep.Networking
{
    public abstract class PacketHandler<T> : Singleton<PacketHandler<T>>, IDisposable where T : INetSerializable, new()
    {
        DeliveryMethod deliveryMethod;

        public PacketHandler(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        {
            this.deliveryMethod = deliveryMethod;

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
                netPacketProcessor.RemoveSubscription<T>();
            }
        }

        // This has to be invoked manually in each Packet Handler inheritor
        protected void RequestSend(T packet)
        {
            Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, deliveryMethod, FikaBackendUtils.IsServer);

            if (FikaBackendUtils.IsServer)
            {
                OnReceive(packet);
            }
        }

        private void BroadcastAndReceive(T packet)
        {
            Plugin.Logger.LogInfo("BroadcastAndReceive");
            if (FikaBackendUtils.IsServer)
            {
                Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, deliveryMethod, true);
            }

            OnReceive(packet);
        }

        public abstract void OnReceive(T packet);
    }
}