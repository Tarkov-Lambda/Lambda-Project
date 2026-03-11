using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base.RateLimiting;
using System;
using System.Diagnostics;

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

        private readonly TokenBucketRateLimiter<int> _serverRateLimiter = new();

        public PacketHandler(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered, PacketAuthority authority = PacketAuthority.Both)
        {
            this.deliveryMethod = deliveryMethod;
            this.authority = authority;

            H.OnGameStarted += RegisterPacket;
            H.OnGameDispose += UnregisterPacket;
            // Hot-reload
            RegisterPacket(H.GameWorld);
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
                _serverRateLimiter.Clear();

                if (H.NetPacketProcessor == null) return;

                H.NetPacketProcessor.RemoveSubscription<T>();
                H.NetPacketProcessor.RemoveSubscription<RejectedPacket<T>>();
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

        private bool IsUnauthorized(int id)
        {
            return authority == PacketAuthority.ServerOnly && !H.MainPlayerScore.IsAdmin;
        }

        // ENTRY POINT
        // SERVER ONLY: If a peer is provided, we will not broadcast and only send it to that peer.
        protected void RequestSend(T packet, NetPeer targetPeer = null)
        {
            if (!H.isInRaid()) return;
            if (IsUnauthorized(H.MainPlayer.Id)) return;
            if (targetPeer != null && FikaBackendUtils.IsClient)
            {
                H.Notify("A Client can not send a packet to a specific peer.");
            }

            if (targetPeer != null)
            {
                // Unicast — send only to the specified peer, do not handle locally
                H.FikaNet.SendDataToPeer(ref packet, deliveryMethod, targetPeer);
            }
            else
            {
                // Broadcast (original behavior)
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
        }

        // 
        protected void RequestSendToPlayer(T packet, int netId)
        {
            if (!H.isInRaid()) return;

            if (netId == H.FikaNet.NetId)
            {
                // We are the target — execute locally
                WhenApproved(packet, null);
                return;
            }

            var peer = H.NetManager.GetPeerById(netId) as NetPeer;
            RequestSend(packet, peer);
        }

        private void WhenServerReceivesPacket(T packet, NetPeer netPeer)
        {
            if (!TryPassServerRateLimit(packet, netPeer))
                return;

            // idk what the best action here is, but for now we just drop
            if (IsUnauthorized(netPeer.Id))
            {
                Plugin.Logger.LogInfo("Unauthorized Packet, dropping");
                return;
            }

            bool validPacket = ServerValidation(ref packet, netPeer);
            if (!validPacket)
            {
                if (H.NetPacketProcessor != null)
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

        protected virtual RateLimitConfig ServerRateLimit => RateLimitConfig.Default;

        protected virtual void OnRateLimited(T packet, NetPeer netPeer, in RateLimitConfig config)
        {
            H.Log($"Rate-limiting peer {netPeer.Id}, Packet {GetType().Name}");
        }

        private bool TryPassServerRateLimit(T packet, NetPeer netPeer)
        {
            var config = ServerRateLimit;
            if (!config.Enabled)
                return true;

            double nowSeconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

            _serverRateLimiter.Prune(nowSeconds, config.StateTtlSeconds);

            bool allowed = _serverRateLimiter.TryConsume(netPeer.Id, nowSeconds, config, out bool canSendReject);
            if (allowed)
                return true;

            OnRateLimited(packet, netPeer, config);


            switch (config.Action)
            {
                case RateLimitAction.Drop:
                    return false;

                case RateLimitAction.Reject:
                    {
                        if (!canSendReject)
                            return false;

                        if (H.NetPacketProcessor == null)
                            return false;

                        var rejected = new RejectedPacket<T> { Payload = packet };
                        H.FikaNet.SendDataToPeer(ref rejected, deliveryMethod, netPeer);
                        return false;
                    }

                case RateLimitAction.Disconnect:
                    try
                    {
                        netPeer.Disconnect();
                    }
                    catch
                    {
                        // oh well
                    }
                    return false;

                default:
                    return false;
            }
        }

        public virtual bool ServerValidation(ref T packet, NetPeer netPeer)
        {
            return true;
        }

        public virtual void ClientPrediction(T packet) { }

        public abstract void WhenApproved(T packet, NetPeer netPeer);

        // Whenever the loca
        public virtual void WhenRejected(T packet, NetPeer netPeer) { }
    }
}