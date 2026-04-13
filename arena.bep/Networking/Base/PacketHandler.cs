using Comfort.Common;
using EFT;
using Fika.Core.Modding.Events;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler.RateLimiting;
using ifp.arena.bep.networking.TimeSync;
using System;
using System.Diagnostics;
using static Fika.Core.Modding.FikaEventDispatcher;
using ifp.arena.bep.networking;

namespace PacketHandler;

public enum PacketAuthority
{
    Both,       // Anyone can send/receive
    Admin,      // Server or Admin
    ServerOnly  // Only Server can send. Clients only receive.
}

public struct RejectionPacket<T> : INetSerializable where T : INetSerializable, new()
{
    public T Payload;
    public string reason;

    public void Serialize(NetDataWriter writer)
    {
        Payload.Serialize(writer);
        writer.Put(reason);
    }

    public void Deserialize(NetDataReader reader)
    {
        Payload = new T();
        Payload.Deserialize(reader);
        reason = reader.GetString();
    }
}

public abstract class PacketHandler<T> : IDisposable where T : INetSerializable, new()
{
    protected DeliveryMethod deliveryMethod;
    protected PacketAuthority authority;

    private readonly TokenBucketRateLimiter<int> _serverRateLimiter = new(); // OPTIONAL
    protected virtual RateLimitConfig ServerRateLimit => RateLimitConfig.Default; // OPTIONAl

    public static event Action<T> OnWhenApprovedPacket; // Misleading because this happens AFTER the execution of WhenApproved

    protected PacketHandler(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered, PacketAuthority authority = PacketAuthority.Both)
    {
        this.deliveryMethod = deliveryMethod;
        this.authority = authority;

        Inititalize();
    }

    protected virtual void Inititalize()
    {
        OnFikaEvent += ManageFikaEvent;

        if (H.IsInRaid() && H.FikaNet != null) RegisterPacket();
    }

    public virtual void Dispose()
    {
        OnFikaEvent -= ManageFikaEvent;

        if (H.IsInRaid() && H.FikaNet != null) UnregisterPacket();
        // Singleton slot cleanup is handled by Plugin.RegisterSingleton via a stored release delegate.
    }

    protected void ManageFikaEvent(FikaEvent fikaEvent)
    {
        if (this is PlayerKilledPacketHandler) D.Log($"Fika Event: {fikaEvent.GetType().Name}");

        if (fikaEvent is FikaNetworkManagerCreatedEvent) RegisterPacket();
        if (fikaEvent is FikaNetworkManagerDestroyedEvent) UnregisterPacket();
    }

    protected void RegisterPacket(GameWorld gWorld = null) => RegisterPacket();

    protected void RegisterPacket()
    {
        D.Log($"Registering {typeof(T).Name}");
        if (H.IsServer)
        {
            H.FikaNet.RegisterPacket<T, NetPeer>(WhenServerReceivesPacket);
            H.FikaNet.RegisterPacket<RejectionPacket<T>, NetPeer>((packet, peer) => { }); // Bro thought he was gonna reject the server
        }
        else
        {
            H.FikaNet.RegisterPacket<T, NetPeer>(WhenClientReceivesPacket);
            H.FikaNet.RegisterPacket<RejectionPacket<T>, NetPeer>(WhenClientReceivesRejection);
        }
    }

    protected void UnregisterPacket(GameWorld gWorld = null) => UnregisterPacket();

    protected void UnregisterPacket()
    {
        try
        {
            _serverRateLimiter.Clear();
            H.NetPacketProcessor.RemoveSubscription<T>();
            H.NetPacketProcessor.RemoveSubscription<RejectionPacket<T>>();
        }
        catch (Exception ex)
        {
            D.Log($"Packet Unregistration Failed: {ex}");
        }
    }

    // Admins have the same authority as the server
    protected bool IsUnauthorized(int id)
    {
        if (H.IsServer) return false;
        
        if (authority == PacketAuthority.Admin)
        {
            PlayerScore score = H.GetPlayerScore(id);
            return score == null || !score.IsAdmin; // unauthorized only if NOT admin
        }
        else if (authority == PacketAuthority.ServerOnly && id != H.MainPlayer.Id)
        {
            return true;
        }

        return false;
    }

    protected void DispatchPacketToPeer(T packet, NetPeer peer)
    {
        if (!H.IsInRaid()) return;
        DispatchPacket(packet, peer);
    }


    // ENTRY POINT
    // SERVER ONLY: If a peer is provided, we will not approve-locally/broadcast and instead only send it to that peer.
    protected void DispatchPacket(T packet, NetPeer targetPeer = null)
    {
        if (!H.IsInRaid()) return;
        if (!H.IsHeadless)
            if (IsUnauthorized(H.MainPlayer.Id)) return;

        if (this is not TimeSynchronizationPacketHandler)
            D.Log($"Sending {typeof(T).Name} at {DateTime.UtcNow}");

        // These are helper boxer/unboxers but overall hurt performance, avoid in high frequency
        // Note that this does not apply to the server generated packets due to the fact that sometimes we will send the packet FOR a player.
        if (packet is IAuthoredPacket authoredPacket && !H.IsHeadless)
        {
            if (authoredPacket.Player == null) authoredPacket.Player = H.MainPlayer;
            packet = (T)(object)authoredPacket;
        }

        // Only auto-stamp for broadcasts. Targeted sends (targetPeer != null) preserve
        // the caller-provided timestamp (e.g. ServerPhaseStartSeconds for late joiners).
        if (packet is IServerTimestampedPacket serverTimestampedPacket && H.IsServer && targetPeer == null)
        {
            serverTimestampedPacket.Timestamp = NetworkTime.ServerNowSeconds;
            packet = (T)(object)serverTimestampedPacket;
        }

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

    protected void WhenServerReceivesPacket(T packet, NetPeer peer)
    {
        if (this is not TimeSynchronizationPacketHandler)
            D.Log($"Receiving {typeof(T).Name} at {NetworkTime.ServerNowSeconds} from Peer {peer.Id}");

        if (!TryPassServerRateLimit(packet, peer))
            return;

        // idk what the best action here is, but for now we just drop
        if (IsUnauthorized(peer.Id))
        {
            D.Log("Unauthorized Packet, dropping");
            return;
        }

        if (!SanitizeMetadata(ref packet, peer))
        {
            SendRejection(ref packet, peer);
            return;
        }

        if (!PacketValidation(ref packet, peer))
        {
            SendRejection(ref packet, peer);
            return;
        }

        if (ShouldBroadcastPacket(packet))
        {
            H.FikaNet.SendData(ref packet, deliveryMethod, true);
        }

        WhenApproved(packet, peer);
        OnWhenApprovedPacket?.Invoke(packet);
    }

    protected void WhenClientReceivesPacket(T packet, NetPeer netPeer)
    {
        if (!H.IsInRaid() || H.FikaNet == null) return;

        if (this is not TimeSyncResponsePacketHandler)
            D.Log($"Receiving {typeof(T).Name} at {NetworkTime.ServerNowSeconds} from Server");

        WhenApproved(packet, netPeer);
        OnWhenApprovedPacket?.Invoke(packet);
    }

    protected void SendRejection(ref T packet, NetPeer peer, string reason = "")
    {
        var rejected = new RejectionPacket<T> { Payload = packet, reason = reason };
        H.FikaNet.SendDataToPeer(ref rejected, deliveryMethod, peer);
    }

    protected void WhenClientReceivesRejection(RejectionPacket<T> rejectedPacket, NetPeer netPeer)
    {
#if DEBUG
        D.Log($"Server Rejected {GetType().Name}");
        if (rejectedPacket.reason != "")
            D.Log(rejectedPacket.reason);
#endif
        WhenRejected(rejectedPacket.Payload, netPeer);
    }

    protected virtual void OnRateLimited(T packet, NetPeer netPeer, in RateLimitConfig config)
    {
        D.Log($"Rate-limiting peer {netPeer.Id}, Packet {GetType().Name}");
    }

    protected bool TryPassServerRateLimit(T packet, NetPeer peer)
    {
        var config = ServerRateLimit;
        if (!config.Enabled)
            return true;

        double nowSeconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        _serverRateLimiter.Prune(nowSeconds, config.StateTtlSeconds);

        bool allowed = _serverRateLimiter.TryConsume(peer.Id, nowSeconds, config, out bool canSendReject);
        if (allowed)
            return true;

        OnRateLimited(packet, peer, config);


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


                    SendRejection(ref packet, peer);
                    return false;
                }

            case RateLimitAction.Disconnect:
                peer.Disconnect();
                return false;

            default:
                return false;
        }
    }

    // OPTIONAL
    protected virtual bool ShouldBroadcastPacket(T packet) => true;

    // OPTIONAL
    // core Generic packet validation
    // runs before packet specific validation
    protected virtual bool SanitizeMetadata(ref T packet, NetPeer netPeer)
    {
        if (packet is IAuthoredPacket authoredPacket)
        {
            // var isPlayerFound = H.FikaNet.CoopHandler.Players.TryGetValue(netPeer.Id, out FikaPlayer playerToApply);
            // D.Log(playerToApply.Id.ToString());
            // if (isPlayerFound == false) return false;

            // // Anti-spoofing
            // if (playerToApply != authoredPacket.Player)
            // {
            //     authoredPacket.Player = playerToApply;
            //     packet = (T)(object)authoredPacket;
            //     return false;
            // }
        }

        if (packet is IServerTimestampedPacket serverTimestampedPacket)
        {
            serverTimestampedPacket.Timestamp = NetworkTime.ServerNowSeconds;
            packet = (T)(object)serverTimestampedPacket;
        }

        return true;
    }


    // optional packet validation
    protected virtual bool PacketValidation(ref T packet, NetPeer peer) { return true; }

    // OPTIONAL
    // In case client is quite sure that the packet is gonna get approved
    // and we want to do sfx/vfx without delay
    protected virtual void LocalPredictApproved(T packet) { }

    // ENTRY POINT
    // packet type specific way of applying the received packet
    protected abstract void WhenApproved(T packet, NetPeer netPeer);

    // OPTIONAL
    // kinda only using this to notify or negate anything done in ClientPrediction
    protected virtual void WhenRejected(T packet, NetPeer netPeer) { }
}