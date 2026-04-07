using Comfort.Common;
using Comfort.Logs;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking.Base.RateLimiting;
using ifp.arena.bep.networking.TimeSync;
using System;
using System.Diagnostics;
using static Fika.Core.Modding.FikaEventDispatcher;

namespace ifp.arena.bep.networking.Base;

public enum PacketAuthority
{
    Both,       // Anyone can send/receive
    Admin,      // Server or Admin
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

// Currently still a lot of pit falls in packet traversal route
// Note: currently the responsibility between ShouldBroadcastClientPacket and RequestSendToPlayer is kind of blurred
// this is probably the first place for refactoring
public abstract class PacketHandler<T> : Singleton<PacketHandler<T>>, IDisposable where T : INetSerializable, new()
{
    protected DeliveryMethod deliveryMethod;
    protected PacketAuthority authority;

    private readonly TokenBucketRateLimiter<int> _serverRateLimiter = new(); // OPTIONAL
    protected virtual RateLimitConfig ServerRateLimit => RateLimitConfig.Default; // OPTIONAl

    public PacketHandler(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered, PacketAuthority authority = PacketAuthority.Both)
    {
        this.deliveryMethod = deliveryMethod;
        this.authority = authority;

        Inititalize();
    }

    public virtual void Inititalize()
    {
        OnFikaEvent += ManageFikaEvent;

        if (H.isInRaid() && H.FikaNet != null) RegisterPacket();
    }

    public void Dispose()
    {
        OnFikaEvent -= ManageFikaEvent;

        if (H.isInRaid() && H.FikaNet != null) UnregisterPacket();
        Release(this);
    }

    public void ManageFikaEvent(FikaEvent fikaEvent)
    {
        if (this is SessionInfoPacketHandler) D.Log($"Fika Event: {fikaEvent.GetType().Name}");

        if (fikaEvent is FikaNetworkManagerCreatedEvent) RegisterPacket();
        if (fikaEvent is FikaNetworkManagerDestroyedEvent) UnregisterPacket();
    }


    public void RegisterPacket() => RegisterPacket(null);

    public void RegisterPacket(GameWorld gWorld = null)
    {
        D.Log($"Registering {typeof(T).Name}");
        if (H.IsServer)
        {
            H.FikaNet.RegisterPacket<T, NetPeer>(WhenServerReceivesPacket);
            H.FikaNet.RegisterPacket<RejectedPacket<T>, NetPeer>((packet, peer) => { }); // Bro thought he was gonna reject the server
        }
        else
        {
            H.FikaNet.RegisterPacket<T, NetPeer>(WhenClientReceivesPacket);
            H.FikaNet.RegisterPacket<RejectedPacket<T>, NetPeer>(WhenClientReceivesRejection);
        }
    }

    public void UnregisterPacket() => UnregisterPacket(null);

    private void UnregisterPacket(GameWorld gWorld = null)
    {
        try
        {
            _serverRateLimiter.Clear();
            if (H.NetPacketProcessor == null) return;
            H.NetPacketProcessor.RemoveSubscription<T>();
            H.NetPacketProcessor.RemoveSubscription<RejectedPacket<T>>();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"ClearPacketSubscriptions failed: {ex}");
        }
    }

    // Admins have the same authority as the server
    private bool IsUnauthorized(int id)
    {
        return authority == PacketAuthority.ServerOnly && !H.GetPlayerScore(id).IsAdmin;
    }

    // OPTIONAL ENTRY POINT
    // SERVER ONLY: Some packets will choose to use this (like bomb assignment, admin auth)
    protected void RequestSendToPlayer(T packet, int netId)
    {
        if (!H.isInRaid()) return;

        // local sender is the target, execute locally; I am not sure how I want to do this
        // But for the sake of keeping things as coupled as possible with the network layer
        // this might come handy later.
        if (netId == H.FikaNet.NetId)
        {
            WhenApproved(packet, null);
            return;
        }

        var peer = H.NetManager.GetPeerById(netId) as NetPeer;
        RequestSend(packet, peer);
    }

    // ENTRY POINT
    // SERVER ONLY: If a peer is provided, we will not approve-locally/broadcast and instead only send it to that peer.
    protected void RequestSend(T packet, NetPeer targetPeer = null)
    {
        if (!H.isInRaid()) return;
        if (IsUnauthorized(H.MainPlayer.Id)) return; // Soft check local-side

        D.Log($"Sending {typeof(T).Name} at {DateTime.UtcNow}");

        // These are helper boxer/unboxers but overall hurt performance, avoid in high frequency 
        if (packet is AuthoredPacket authoredPacket)
        {
            if (authoredPacket.player == null) authoredPacket.player = H.MainPlayer;
            packet = (T)(object)authoredPacket;
        }

        if (packet is ServerTimestampedPacket serverTimestampedPacket && H.IsServer)
        {
            serverTimestampedPacket.timestamp = NetworkTime.ServerNowSeconds;
            packet = (T)(object)serverTimestampedPacket;
        }

        // targetPeer will never be local here
        if (targetPeer != null)
        {
            H.FikaNet.SendDataToPeer(ref packet, deliveryMethod, targetPeer);
        }
        else
        {
            LocalPredictApproved(packet);

            H.FikaNet.SendData(ref packet, deliveryMethod, H.IsServer);
            if (H.IsServer)
            {
                // WhenServerReceivesPacket(packet, Singleton<NetPeer>.Instance);
                WhenApproved(packet, Singleton<NetPeer>.Instance);
            }
        }
    }

    private void WhenServerReceivesPacket(T packet, NetPeer netPeer)
    {
        if (this is HandsInspectPacketHandler)
        {
            D.Notify($"Inspecting");
            D.Notify($"{netPeer.Id}");

        }

        if (!TryPassServerRateLimit(packet, netPeer))
            return;

        // idk what the best action here is, but for now we just drop
        if (IsUnauthorized(netPeer.Id))
        {
            D.Log("Unauthorized Packet, dropping");
            return;
        }

        // If ServerValidation returns false, send reject packet and return before doing applying the packet.
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

        if (ShouldBroadcastPacket(packet)) // if this packet originates from the server - we already broadcasted it
        {
            H.FikaNet.SendData(ref packet, deliveryMethod, true);
        }

        WhenApproved(packet, netPeer);
    }

    private void WhenClientReceivesPacket(T packet, NetPeer netPeer)
    {
        if (!H.isInRaid() || H.FikaNet == null) return;

        WhenApproved(packet, netPeer);
    }

    private void WhenClientReceivesRejection(RejectedPacket<T> rejectedPacket, NetPeer netPeer)
    {
        WhenRejected(rejectedPacket.Payload, netPeer);
    }

    protected virtual void OnRateLimited(T packet, NetPeer netPeer, in RateLimitConfig config)
    {
        D.Log($"Rate-limiting peer {netPeer.Id}, Packet {GetType().Name}");
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
                netPeer.Disconnect();
                return false;

            default:
                return false;
        }
    }

    // OPTIONAL
    protected virtual bool ShouldBroadcastPacket(T packet) => true;

    // OPTIONAL
    // For hard checking client packets
    /// <summary>returning false means the packet is rejected</summary>
    protected virtual bool ServerValidation(ref T packet, NetPeer netPeer)
    {
        if (packet is AuthoredPacket authoredPacket)
        {
            // Anti-spoofing
            // if (authoredPacket.player.Id != netPeer.Id)
            // {
            //     return false;
            // }
        }

        if (packet is ServerTimestampedPacket serverTimestampedPacket)
        {
            serverTimestampedPacket.timestamp = NetworkTime.ServerNowSeconds;
            packet = (T)(object)serverTimestampedPacket;
        }

        return true;
    }

    // OPTIONAL
    // In case client is quite sure that the packet is gonna get approved
    // and we want to do sfx/vfx without delay
    protected virtual void LocalPredictApproved(T packet) { }

    // ENTRY POINT
    // packet type specific way of applying the received packet
    protected abstract void WhenApproved(T packet, NetPeer netPeer);

    // OPTIONAL
    // kinda only using this to notify or negate anything done in ClientPrediction
    protected virtual void WhenRejected(T packet, NetPeer netPeer)
    {
        D.Log($"Server Rejected the packet: {GetType().Name}");
    }
}