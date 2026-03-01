using Comfort.Common;
using EFT;
using EFT.UI;
using Fika.Core.ConsoleCommands;
using Fika.Core.Main.Components;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using FlyingWormConsole3.LiteNetLib;
using HarmonyLib;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Patches.Tarkov;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using static GClass1485;
using DeliveryMethod = Fika.Core.Networking.LiteNetLib.DeliveryMethod;

namespace ifp.arena.bep.Networking.Base
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
            Plugin.Logger.LogInfo($"Registering {typeof(T).Name}");
            if(FikaBackendUtils.IsServer)
            {
                Singleton<IFikaNetworkManager>.Instance.RegisterPacket<T, NetPeer>(BroadcastAndReceive);
            } else
            {
                Singleton<IFikaNetworkManager>.Instance.RegisterPacket<T, NetPeer>(OnReceive);

            }
        }

        public void Dispose()
        {
            Plugin.Logger.LogInfo($"Disposing {typeof(T).FullName}");

            Patch_Gameworld_OnGameStarted.OnGameStarted -= RegisterPacket;

            try
            {
                var manager = Singleton<IFikaNetworkManager>.Instance;

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
        // Server Send -> Server RequestSend -> Server Broadcast Packet -> Server/Client OnReceive
        // Client Send -> Client RequestSend -> Server BroadcastAndReceive -> Server ServerValidation -> Server Broadcast Packet -> Server/Client OnReceive

        protected void RequestSend(T packet)
        {
            if (authority == PacketAuthority.ServerOnly && !FikaBackendUtils.IsServer)
            {
                return;
            }

            Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, deliveryMethod, FikaBackendUtils.IsServer);

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

            Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, deliveryMethod, true);

            OnReceive(packet, netPeer);
        }

        public virtual bool ServerValidation(ref T packet, NetPeer netPeer)
        {
            return true;
        }
        
        public abstract void OnReceive(T packet, NetPeer netPeer);
    }
}