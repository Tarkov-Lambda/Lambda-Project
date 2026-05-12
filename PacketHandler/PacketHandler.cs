using System;
using System.Diagnostics;
using EFT;
using MemoryPack;
using PacketHandler.RateLimiting;

namespace PacketHandler;

public enum PacketAuthority
{
    Anyone,     // Anyone can send/receive
    Admin,      // Server or Admin
    ServerOnly  // Only Server can send. Clients only receive.
}

[MemoryPackable]
public partial struct RejectionPacket<T> : IPacket where T : IPacket, new()
{
    public T Payload;
    public string rejectionReason;
}

public abstract class PacketHandler<T> : IDisposable where T : IPacket, new()
{
    private readonly TokenBucketRateLimiter<int> _serverRateLimiter = new();

    protected virtual RateLimitConfig ServerRateLimit => RateLimitPresets.Disabled; 

    protected virtual bool ShouldLog => true; 
    protected virtual bool ShouldNotifyAboutRejection => false; 

    protected virtual bool ShouldProcessInstantly => true;

    protected virtual DeliveryType DeliveryType => DeliveryType.ReliableOrdered;

    protected virtual PacketAuthority Authority => PacketAuthority.Anyone;

    public static event Action<T> BeforePacketApplied;
    public static event Action<T> AfterPacketApplied;

    protected PacketHandler() => Initialize();

    protected virtual void Initialize()
    {
        H.OnNetworkCreated += RegisterPacket;
        H.OnNetworkDestroyed += UnregisterPacket;

        if (H.IsInRaid() && H.Network != null) RegisterPacket();
    }

    public virtual void Dispose()
    {
        H.OnNetworkCreated -= RegisterPacket;
        H.OnNetworkDestroyed -= UnregisterPacket;

        if (H.IsInRaid() && H.Network != null) UnregisterPacket();
    }

    protected void RegisterPacket()
    {
        H.Log($"Registering {typeof(T).Name}");
        
        H.Network.RegisterPacketHandler<T>(WhenReceivedInternal);
        H.Network.RegisterPacketHandler<RejectionPacket<T>>(WhenRejectionReceivedInternal);
    }

    protected void UnregisterPacket()
    {
        try
        {
            _serverRateLimiter.Clear();
            H.Network.UnregisterPacketHandler<T>();
            H.Network.UnregisterPacketHandler<RejectionPacket<T>>();
        }
        catch (Exception ex)
        {
            H.Log($"Packet Unregistration Failed: {ex}");
        }
    }

    private void WhenReceivedInternal(T packet, int peerId)
    {
        if (H.Network.IsServer)
            WhenServerReceivesPacket(packet, peerId);
        else
            WhenClientReceivesPacket(packet, peerId);
    }

    private void WhenRejectionReceivedInternal(RejectionPacket<T> packet, int peerId)
    {
        if (H.Network.IsClient)
            WhenClientReceivesRejection(packet, peerId);
    }

    protected virtual bool IsUnauthorized(int id) => false;

    // SERVER ONLY
    protected void DispatchPacketToPeer(T packet, int peerId)
    {
        if (!H.IsInRaid()) return;
        DispatchPacket(packet, peerId);
    }

    // SERVER ONLY
    protected void DispatchPacketToPlayer(T packet, Player player)
    {
        if (!H.IsInRaid() || player.IsAI) return;

        int peerId = H.GetPeerIdByPlayer(player);
        DispatchPacket(packet, peerId);
    }

    // ENTRY POINT
    protected void DispatchPacket(T packet, int? targetPeerId = null)
    {
        if (!H.IsInRaid()) return;

#if DEBUG
        if (H.GameWorld is HideoutGameWorld)
        {
            LocalPredictApproved(packet);
            ApplyInternal(packet, 0);
            return;
        }
#endif

        if (!H.IsHeadless && IsUnauthorized(H.MainPlayer.Id)) return;

#if DEBUG
        if (ShouldLog) H.Log($"Sending {typeof(T).Name} at {DateTime.UtcNow}");
#endif

        LocalPredictApproved(packet);

        if (H.Network.IsClient)
        {
            H.Network.SendData(ref packet, DeliveryType, false);
        }
        else if (targetPeerId == null)
        {
            ProcessApprovedPacket(ref packet, 0); // Server generated
        }
        else
        {
            MutateApprovedPacket(ref packet, targetPeerId.Value);
            H.Network.SendDataToPeer(ref packet, DeliveryType, targetPeerId.Value);
        }
    }

    protected void WhenServerReceivesPacket(T packet, int peerId)
    {
#if DEBUG
        if (ShouldLog) H.Log($"Receiving {typeof(T).Name} from Peer {peerId}");
#endif

        if (!TryPassServerRateLimit(packet, peerId))
            return;

        if (IsUnauthorized(peerId))
        {
            SendRejection(ref packet, peerId, $"You are not authorized to send {typeof(T).Name}");
            return;
        }

        if (!SanitizeMetadata(ref packet, peerId, out string sanitizationRejectionReason))
        {
            SendRejection(ref packet, peerId, sanitizationRejectionReason);
            return;
        }

        if (!ValidatePacket(packet, peerId, out string rejectionReason))
        {
            SendRejection(ref packet, peerId, rejectionReason);
            return;
        }

        ProcessApprovedPacket(ref packet, peerId);
    }

    protected virtual void ProcessApprovedPacket(ref T packet, int peerId)
    {
        MutateApprovedPacket(ref packet, peerId);
        H.Network.SendData(ref packet, DeliveryType, true); // Broadcast
        ApplyInternal(packet, peerId);
    }

    protected virtual void MutateApprovedPacket(ref T packet, int peerId) { }

    protected void ApplyInternal(T packet, int peerId)
    {
        TryInvokeAction(BeforePacketApplied, packet);
        Apply(packet, peerId);
        TryInvokeAction(AfterPacketApplied, packet);
    }

    protected virtual void WhenClientReceivesPacket(T packet, int peerId)
    {
#if DEBUG
        if (ShouldLog) H.Log($"Receiving {typeof(T).Name} from Server");
#endif
        ApplyInternal(packet, peerId);
    }

    private void TryInvokeAction(Action<T> action, T packet)
    {
        try
        {
            action?.Invoke(packet);
        }
        catch (Exception e)
        {
            H.Log($"Error in {GetType().Name}'s subscriber: {e.Message}\n{e.StackTrace}");
        }
    }

    protected void SendRejection(ref T packet, int peerId, string rejectionReason = null)
    {
        var rejected = new RejectionPacket<T> { Payload = packet, rejectionReason = rejectionReason };
        H.Network.SendDataToPeer(ref rejected, DeliveryType, peerId);
    }

    protected void WhenClientReceivesRejection(RejectionPacket<T> rejectedPacket, int peerId)
    {
        if (ShouldLog)
        {
            H.Log($"Server Rejected {GetType().Name}");
            if (!string.IsNullOrEmpty(rejectedPacket.rejectionReason))
                H.Log(rejectedPacket.rejectionReason);
        }

        if (ShouldNotifyAboutRejection && !string.IsNullOrEmpty(rejectedPacket.rejectionReason))
        {
            H.Notify(rejectedPacket.rejectionReason);
        }

        WhenRejected(rejectedPacket.Payload, peerId);
    }

    protected virtual void OnRateLimited(T packet, int peerId, in RateLimitConfig config)
    {
        H.Log($"Rate-limiting peer {peerId}, Packet {GetType().Name}");
    }

    protected bool TryPassServerRateLimit(T packet, int peerId)
    {
        var config = ServerRateLimit;
        if (!config.Enabled)
            return true;

        double nowSeconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        _serverRateLimiter.Prune(nowSeconds, config.StateTtlSeconds);

        bool allowed = _serverRateLimiter.TryConsume(peerId, nowSeconds, config, out bool canSendReject);
        if (allowed) return true;

        OnRateLimited(packet, peerId, config);

        switch (config.Action)
        {
            case RateLimitAction.Drop:
                return false;
            case RateLimitAction.Reject:
                if (canSendReject) SendRejection(ref packet, peerId);
                return false;
            case RateLimitAction.Disconnect:
                H.Network.DisconnectPeer(peerId);
                return false;
            default:
                return false;
        }
    }

    protected virtual bool SanitizeMetadata(ref T packet, int peerId, out string rejectionReason)
    {
        if (packet is IAuthoredPacket authoredPacket)
        {
            if (authoredPacket.Player == null)
            {
                rejectionReason = "Unknown or null player";
                return false;
            }

            // We need a way to resolve the peerId back to the player object to verify spoofing
            Player senderPlayer = H.GetPlayerByPeerId(peerId);
            if (authoredPacket.Player != senderPlayer)
            {
                rejectionReason = "You can't send packets for other players";
                return false;
            }
        }

        rejectionReason = null;
        return true;
    }

    protected virtual bool ValidatePacket(T packet, int peerId, out string rejectionReason)
    {
        rejectionReason = string.Empty;
        return true;
    }

    protected virtual void LocalPredictApproved(T packet) { }

    protected abstract void Apply(T packet, int peerId);

    protected virtual void WhenRejected(T packet, int peerId) { }
}