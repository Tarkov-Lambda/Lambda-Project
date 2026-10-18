using System;
using System.Diagnostics;
using EFT;
using MemoryPack;
using PacketWarden.RateLimiting;
using PacketWarden.TimeSync;

namespace PacketWarden;

public enum PacketAuthority
{
    Anyone,
    Admin,
    ServerOnly,
}

[MemoryPackable]
public partial struct RejectionPacket<T> : IPacket
    where T : IPacket, new()
{
    public T Payload;
    public string rejectionReason;
}

public abstract class PacketWarden<T> : IDisposable
    where T : IPacket, new()
{
    private static readonly string UnauthorizedReason =
        $"You are not authorized to send {typeof(T).Name}";
    private static readonly string RateLimitedReason =
        $"You've been rate limited. ({typeof(T).Name})";

    protected INetworkBackend Network => Plugin.Network;

    public bool IsRegistered { get; private set; }

    private readonly TokenBucketRateLimiter<int> _serverRateLimiter = new();

    #region Configuration
    // Rate limiting runs when LNL hands the packet over, so ReliableOrdered packets held back
    // behind a lost packet arrive in a burst and may be limited. In most cases this is fine.
    protected virtual RateLimitConfig ServerRateLimit => RateLimitPresets.Disabled;

    protected virtual DeliveryType DeliveryType => DeliveryType.ReliableOrdered;
    protected virtual PacketAuthority Authority => PacketAuthority.Anyone;

    protected virtual bool ShouldLog => false;
    protected virtual bool ShouldNotifyAboutRejection => false;
    #endregion

    #region Callbacks
    public event Action<T> BeforePacketAppliedOptimistically;
    public event Action<T> AfterPacketAppliedOptimistically;

    public event Action<T> BeforePacketApplied;
    public event Action<T> AfterPacketApplied;
    #endregion

    #region Lifecycle
    protected PacketWarden() => Initialize();

    protected virtual void Initialize()
    {
        Network.OnNetworkCreated += RegisterPacket;
        Network.OnNetworkDestroyed += UnregisterPacket;
        Network.OnPeerDisconnected += _serverRateLimiter.Remove;

        if (H.IsInRaid())
            RegisterPacket(); // Hot reloading
    }

    public virtual void Dispose()
    {
        Network.OnNetworkCreated -= RegisterPacket;
        Network.OnNetworkDestroyed -= UnregisterPacket;
        Network.OnPeerDisconnected -= _serverRateLimiter.Remove;

        if (IsRegistered)
            UnregisterPacket(); // Hot reloading
    }

    private void RegisterPacket()
    {
        Network.RegisterPacketWarden<T>(WhenReceivedInternal);
        Network.RegisterPacketWarden<RejectionPacket<T>>(WhenRejectionReceivedInternal);

        IsRegistered = true;
    }

    private void UnregisterPacket()
    {
        _serverRateLimiter.Clear();

        Network.UnregisterPacketWarden<T>();
        Network.UnregisterPacketWarden<RejectionPacket<T>>();

        IsRegistered = false;
    }
    #endregion

    #region Receiving
    private void WhenReceivedInternal(T packet, int peerId)
    {
        if (Network.IsServer)
            WhenServerReceivesPacket(packet, peerId);
        else
            WhenClientReceivesPacket(packet, peerId);
    }

    private void WhenRejectionReceivedInternal(RejectionPacket<T> rejection, int peerId)
    {
        if (Network.IsClient)
            WhenClientReceivesRejection(rejection, peerId);
    }
    #endregion

    #region Sending
    protected void DispatchPacket(ref T packet, int? targetPeerId = null)
    {
        if (!H.IsInRaid())
            return;
        if (IsUnauthorized(INetworkBackend.LocalPeerId))
            return;

#if DEBUG
        if (ShouldLog)
            H.Log(
                $"[PacketWarden] Sending {typeof(T).Name} at {NetworkTime.LocalNowSeconds} {NetworkTime.ServerNowSeconds}"
            );
#endif

        ApplyOptimisticallyInternal(packet);

        if (Network.IsClient)
        {
            Network.SendData(ref packet, DeliveryType);
            return;
        }

        if (targetPeerId == null)
        {
            ProcessApprovedPacket(ref packet, INetworkBackend.LocalPeerId);
            return;
        }

        int peerId = targetPeerId.Value;

        MutateApprovedPacket(ref packet, peerId);

        if (peerId == INetworkBackend.LocalPeerId)
            ApplyInternal(packet, peerId);
        else
            Network.SendDataToPeer(ref packet, DeliveryType, peerId);
    }
    #endregion

    #region Server Pipeline
    protected void WhenServerReceivesPacket(T packet, int peerId)
    {
#if DEBUG
        if (ShouldLog)
            H.Log(
                $"[PacketWarden] Receiving {typeof(T).Name} from Peer {peerId} at {NetworkTime.LocalNowSeconds} {NetworkTime.ServerNowSeconds}"
            );
#endif

        if (!TryPassServerRateLimit(packet, peerId))
            return;

        if (IsUnauthorized(peerId))
        {
            SendRejection(ref packet, peerId, UnauthorizedReason);
            return;
        }

        if (
            !SanitizeMetadata(ref packet, peerId, out string rejectionReason)
            || !ValidatePacket(packet, peerId, out rejectionReason)
        )
        {
            SendRejection(ref packet, peerId, rejectionReason);
            return;
        }

        ProcessApprovedPacket(ref packet, peerId);
    }

    protected bool TryPassServerRateLimit(T packet, int peerId)
    {
        RateLimitConfig config = ServerRateLimit;
        if (!config.Enabled)
            return true;

        double nowSeconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        if (_serverRateLimiter.TryConsume(peerId, nowSeconds, config, out bool notify))
            return true;

        if (notify)
            OnRateLimited(packet, peerId, config);

        switch (config.Action)
        {
            case RateLimitAction.Reject:
                if (notify)
                    SendRejection(ref packet, peerId, RateLimitedReason);
                break;
            case RateLimitAction.Disconnect:
                Network.DisconnectPeer(peerId);
                break;
        }

        return false;
    }

    protected virtual bool IsUnauthorized(int peerId)
    {
        return Authority switch
        {
            PacketAuthority.ServerOnly => !Network.IsServer
                || peerId != INetworkBackend.LocalPeerId,
            PacketAuthority.Admin => !IsAdmin(peerId),
            _ => false,
        };
    }

    protected virtual bool IsAdmin(int peerId) =>
        Network.IsServer && peerId == INetworkBackend.LocalPeerId;

    protected virtual bool SanitizeMetadata(ref T packet, int peerId, out string rejectionReason)
    {
        rejectionReason = null;

        if (AuthoredPacket<T>.GetPlayer == null)
            return true;

        Player author = AuthoredPacket<T>.GetPlayer(packet);

        if (author == null)
        {
            rejectionReason = "Unknown or null player";
            return false;
        }

        if (author != Network.GetPlayerByPeerId(peerId))
        {
            rejectionReason = "You can't send packets for other players";
            return false;
        }

        return true;
    }

    protected virtual bool ValidatePacket(T packet, int peerId, out string rejectionReason)
    {
        rejectionReason = null;
        return true;
    }

    // Override only to defer application or change who receives it. Packet logic belongs in MutateApprovedPacket and Apply.
    protected virtual void ProcessApprovedPacket(ref T packet, int peerId)
    {
        MutateApprovedPacket(ref packet, peerId);
        Network.SendData(ref packet, DeliveryType);
        ApplyInternal(packet, peerId);
    }

    protected virtual void MutateApprovedPacket(ref T packet, int peerId) { }

    protected void SendRejection(ref T packet, int peerId, string rejectionReason = null)
    {
        var rejection = new RejectionPacket<T>
        {
            Payload = packet,
            rejectionReason = rejectionReason,
        };

        if (peerId == INetworkBackend.LocalPeerId)
            WhenClientReceivesRejection(rejection, peerId);
        else
            Network.SendDataToPeer(ref rejection, DeliveryType, peerId);
    }

    protected virtual void OnRateLimited(T packet, int peerId, in RateLimitConfig config)
    {
        H.Log($"Rate-limiting peer {peerId}, Packet {GetType().Name}");
    }
    #endregion

    #region Client Pipeline
    protected virtual void WhenClientReceivesPacket(T packet, int peerId)
    {
#if DEBUG
        if (ShouldLog)
            H.Log(
                $"[PacketWarden] Receiving {typeof(T).Name} from Server at {NetworkTime.LocalNowSeconds} {NetworkTime.ServerNowSeconds}"
            );
#endif
        ApplyInternal(packet, peerId);
    }

    protected void WhenClientReceivesRejection(RejectionPacket<T> rejection, int peerId)
    {
        bool hasReason = !string.IsNullOrEmpty(rejection.rejectionReason);

        if (ShouldLog)
            H.Log(
                hasReason
                    ? $"Server Rejected {GetType().Name}: {rejection.rejectionReason}"
                    : $"Server Rejected {GetType().Name}"
            );

        if (ShouldNotifyAboutRejection && hasReason)
            Notify(rejection.rejectionReason);

        WhenRejected(rejection.Payload, peerId);
    }

    protected virtual void WhenRejected(T packet, int peerId) { }
    #endregion

    #region Application Pipeline
    private void ApplyOptimisticallyInternal(T packet)
    {
        TryInvokeAction(BeforePacketAppliedOptimistically, packet);
        ApplyOptimistically(packet);
        TryInvokeAction(AfterPacketAppliedOptimistically, packet);
    }

    protected void ApplyInternal(T packet, int peerId)
    {
        TryInvokeAction(BeforePacketApplied, packet);
        OptionalBootstrap(packet);
        Apply(packet, peerId);
        TryInvokeAction(AfterPacketApplied, packet);
    }

    // Runs on whoever dispatches, hosting servers included. This should really only be used for SFX/VFX.
    protected virtual void ApplyOptimistically(T packet) { }

    protected abstract void Apply(T packet, int peerId);
    #endregion

    #region Helpers
    private void OptionalBootstrap(T packet)
    {
        if (!NetworkTime.HasSync && Network.IsClient && packet is IServerTimestampedPacket stamped)
        {
            NetworkTime.BootstrapFromServerStamp(stamped.Timestamp);
        }
    }

    protected virtual void Notify(string rejectionReason)
    {
        H.Notify(rejectionReason);
    }

    private void TryInvokeAction(Action<T> action, T packet)
    {
        if (action == null)
            return;

        try
        {
            action(packet);
        }
        catch (Exception e)
        {
            H.Log($"Error in {GetType().Name}'s subscriber: {e}");
        }
    }
    #endregion
}
