using Fika.Core.Modding.Events;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler.RateLimiting;
using ifp.arena.bep.networking.TimeSync;
using System;
using System.Diagnostics;
using static Fika.Core.Modding.FikaEventDispatcher;
using ifp.arena.bep.networking;
using EFT;
using Fika.Core.Main.Players;
using Coffee.UIEffects;

namespace PacketHandler;

public enum PacketAuthority
{
    Anyone,     // Anyone can send/receive
    Admin,      // Server or Admin
    ServerOnly  // Only Server can send. Clients only receive.
}

public struct RejectionPacket<T> : INetSerializable where T : INetSerializable, new()
{
    public T Payload;
    public string rejectionReason;

    public void Serialize(NetDataWriter writer)
    {
        Payload.Serialize(writer);
        writer.Put(rejectionReason);
    }

    public void Deserialize(NetDataReader reader)
    {
        Payload = new T();
        Payload.Deserialize(reader);
        rejectionReason = reader.GetString();
    }
}

public abstract class PacketHandler<T> : IDisposable where T : INetSerializable, new()
{
    protected DeliveryMethod deliveryMethod;
    protected PacketAuthority authority;

    private readonly TokenBucketRateLimiter<int> _serverRateLimiter = new(); // OPTIONAL
    protected virtual RateLimitConfig ServerRateLimit => RateLimitPresets.Default; // OPTIONAl

    protected virtual bool ShouldLog => true; // Debugging
    protected virtual bool ShouldNotifyAboutRejection => false; // Should we surface the rejection reason in the UI?

    protected virtual bool ShouldProcessInstantly => true;

    public static event Action<T> BeforePacketApplied;
    public static event Action<T> AfterPacketApplied;

    protected PacketHandler(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered, PacketAuthority authority = PacketAuthority.Anyone)
    {
        this.deliveryMethod = deliveryMethod;
        this.authority = authority;

        Initialize();
    }

    protected virtual void Initialize()
    {
        OnFikaEvent += ManageFikaEvent;

        if (H.IsInRaid() && H.FikaNet != null) RegisterPacket();
    }

    public virtual void Dispose()
    {
        OnFikaEvent -= ManageFikaEvent;

        if (H.IsInRaid() && H.FikaNet != null) UnregisterPacket();
    }

    protected void ManageFikaEvent(FikaEvent fikaEvent)
    {
#if DEBUG
        if (this is PlayerKilledPacketHandler) D.Log($"Fika Event: {fikaEvent.GetType().Name}");
#endif
        if (fikaEvent is FikaNetworkManagerCreatedEvent) RegisterPacket();
        if (fikaEvent is FikaNetworkManagerDestroyedEvent) UnregisterPacket();
    }

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

    // SERVER ONLY
    protected void DispatchPacketToPeer(T packet, NetPeer peer)
    {
        if (!H.IsInRaid()) return;
        DispatchPacket(packet, peer);
    }

    // SERVER ONLY
    // idk I need this
    protected void DispatchPacketToPlayer(T packet, Player player)
    {
        if (!H.IsInRaid()) return;
        if (player.IsAI) return;

        FikaPlayer fikaPlayer = player as FikaPlayer;
        var peer = H.NetManager.GetPeerById(fikaPlayer.NetId);

        DispatchPacket(packet, peer as NetPeer);
    }

    // ENTRY POINT
    // SERVER ONLY: If a peer is provided, we will not approve-locally/broadcast and instead only send it to that peer.
    protected void DispatchPacket(T packet, NetPeer targetPeer = null)
    {
        if (!H.IsInRaid()) return;

#if DEBUG
        // simulation of the real game inside hideout
        if (H.GameWorld is HideoutGameWorld)
        {
            LocalPredictApproved(packet);
            ApplyInternal(packet, null);
            return;
        }
#endif

        // early nopout from sending packets that we aren't allowed to send anyways
        if (!H.IsHeadless)
        {
            if (IsUnauthorized(H.MainPlayer.Id)) return;
        }

#if DEBUG
        if (ShouldLog) D.Log($"Sending {typeof(T).Name} at {DateTime.UtcNow}");
#endif

        // this function is invoked before any kind of packet mutation
        // inside AfterServerApprovesPacket occurs. make sure nothing stupid is implemented here
        LocalPredictApproved(packet);


        // this is slightly misleading inside this function
        // but sometimes we will send data to another client without ever applying it serverside
        // decision must be made here
        if (H.IsClient)
        {
            H.FikaNet.SendData(ref packet, deliveryMethod, false);
        }
        else
        {
            ProcessApprovedPacket(ref packet, targetPeer);
        }
    }

    protected void WhenServerReceivesPacket(T packet, NetPeer peer)
    {
#if DEBUG
        if (ShouldLog) D.Log($"Receiving {typeof(T).Name} at {NetworkTime.ServerNowSeconds} from Peer {peer.Id}");
#endif

        if (!TryPassServerRateLimit(packet, peer))
            return;

        if (IsUnauthorized(peer.Id))
        {
            SendRejection(ref packet, peer, $"You are not authorized to send {typeof(T).Name}");
            return;
        }

        // making sure interfaced packets are truthful
        if (!SanitizeMetadata(ref packet, peer, out string sanitizationRejectionReason))
        {
            SendRejection(ref packet, peer, sanitizationRejectionReason);
            return;
        }

        // packet specific serverside validation of incoming packets
        if (!ValidatePacket(packet, peer, out string rejectionReason))
        {
            SendRejection(ref packet, peer, rejectionReason);
            return;
        }

        ProcessApprovedPacket(ref packet, peer);
    }

    protected virtual void ProcessApprovedPacket(ref T packet, NetPeer peer)
    {
        MutateApprovedPacket(ref packet, peer);
        H.FikaNet.SendData(ref packet, deliveryMethod, true);
        ApplyInternal(packet, peer);
    }

    // this function is the central place of "mutate right before applying anywhere made by anyone"
    protected virtual void MutateApprovedPacket(ref T packet, NetPeer peer) { }

    protected void ApplyInternal(T packet, NetPeer peer)
    {
        TryInvokeAction(BeforePacketApplied, packet);
        Apply(packet, peer);
        TryInvokeAction(AfterPacketApplied, packet);
    }

    protected void WhenClientReceivesPacket(T packet, NetPeer peer)
    {
        if (!H.IsInRaid() || H.FikaNet == null) return;

#if DEBUG
        if (ShouldLog) D.Log($"Receiving {typeof(T).Name} at {NetworkTime.ServerNowSeconds} from Server");
#endif

        ApplyInternal(packet, peer);
    }

    private void TryInvokeAction(Action<T> action, T packet)
    {
        try
        {
            action?.Invoke(packet);
        }
        catch (Exception e)
        {
            D.Log($"An error has occured in {GetType().Name}'s subscriber");
            D.Log(e.Message);
            D.Log(e.StackTrace);
        }
    }

    // TODO: throttle this per player
    protected void SendRejection(ref T packet, NetPeer peer, string rejectionReason = null)
    {
        var rejected = new RejectionPacket<T> { Payload = packet, rejectionReason = rejectionReason };
        H.FikaNet.SendDataToPeer(ref rejected, deliveryMethod, peer);
    }

    protected void WhenClientReceivesRejection(RejectionPacket<T> rejectedPacket, NetPeer peer)
    {
        if (ShouldLog)
        {
            D.Log($"Server Rejected {GetType().Name}");
            if (!string.IsNullOrEmpty(rejectedPacket.rejectionReason))
            {
                D.Log(rejectedPacket.rejectionReason);
            }
        }

        if (ShouldNotifyAboutRejection && !string.IsNullOrEmpty(rejectedPacket.rejectionReason))
        {
            D.Notify(rejectedPacket.rejectionReason);
        }

        WhenRejected(rejectedPacket.Payload, peer);
    }

    protected virtual void OnRateLimited(T packet, NetPeer peer, in RateLimitConfig config)
    {
        D.Log($"Rate-limiting peer {peer.Id}, Packet {GetType().Name}");
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
    // core sanitization
    protected virtual bool SanitizeMetadata(ref T packet, NetPeer peer, out string rejectionReason)
    {
        // Anti-Spoofing
        if (packet is IAuthoredPacket authoredPacket)
        {
            if (authoredPacket.Player == null)
            {
                rejectionReason = "Unknown or null player";
                return false;
            }

            // if (authoredPacket.Player != peer.Player)
            // {
            //     rejectionReason = "You can't send packets for other players";
            //     return false;
            // }
        }

        rejectionReason = null;
        return true;
    }

    // optional packet validation
    // though this adds a bit of boilerplate, it's a good practice to explain rejection.
    protected virtual bool ValidatePacket(T packet, NetPeer peer, out string rejectionReason)
    {
        rejectionReason = null;
        return true;
    }

    // OPTIONAL
    // In case client is quite sure that the packet is gonna get approved
    // and we want to do sfx/vfx without delay
    protected virtual void LocalPredictApproved(T packet) { }

    // ENTRY POINT
    // packet type specific way of applying the received packet
    protected abstract void Apply(T packet, NetPeer peer);

    // OPTIONAL
    // kinda only using this to notify or negate anything done in ClientPrediction
    protected virtual void WhenRejected(T packet, NetPeer peer) { }
}