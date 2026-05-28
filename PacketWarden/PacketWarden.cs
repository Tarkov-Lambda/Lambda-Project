using System;
using System.Diagnostics;
using EFT;
using MemoryPack;
using PacketWarden.RateLimiting;
using PacketWarden.TimeSync;

namespace PacketWarden;

/// <summary>
/// Defines who has permission to send and receive this packet type.
/// </summary>
public enum PacketAuthority
{
    /// <summary>Any peer (Server or Client) can send and receive this packet.</summary>
    Anyone,
    /// <summary>Only Server or designated Admin players can send this packet.</summary>
    Admin,
    /// <summary>Only the Server can send this packet. Clients will only receive.</summary>
    ServerOnly
}

/// <summary>
/// A wrapper packet used when the server explicitly rejects a client's packet.
/// </summary>
/// <typeparam name="T">The original packet type that was rejected.</typeparam>
[MemoryPackable]
public partial struct RejectionPacket<T> : IPacket where T : IPacket, new()
{
    public T Payload;
    public string rejectionReason;
}

/// <summary>
/// The core abstract manager for a specific network packet. <br/>
/// Handles rate limiting, authority validation, sanitization, server-side broadcasting, <br/>
/// and client-side prediction (optimistic execution). <br/>
/// </summary>
/// <typeparam name="T">The packet struct managed by this warden.</typeparam>
public abstract class PacketWarden<T> : IDisposable where T : IPacket, new()
{
    /// <summary>Provides access to the current network backend.</summary>
    protected INetworkBackend Network => Plugin.Network;

    /// <summary>
    /// Indicates whether this warden is currently registered and actively listening for packets.
    /// </summary>
    public bool IsRegistered { get; private set; } = false;

    private readonly TokenBucketRateLimiter<int> _serverRateLimiter = new();

    /// <summary>Defines the rate limiting policy for this packet on the server side. Defaults to Disabled.</summary>
    protected virtual RateLimitConfig ServerRateLimit => RateLimitPresets.Disabled;

    /// <summary>Whether basic lifecycle events (Sending/Receiving) should be logged.</summary>
    protected virtual bool ShouldLog => false;

    /// <summary>Whether the client should receive an in-game UI notification if their packet is rejected by the server.</summary>
    protected virtual bool ShouldNotifyAboutRejection => false;

    /// <summary>Determines if the packet should be processed the exact frame it arrives. (Handled in inheriting classes if overridden to false).</summary>
    protected virtual bool ShouldProcessInstantly => true;

    /// <summary>The network reliability configuration for this packet (e.g., ReliableOrdered, Unreliable).</summary>
    protected virtual DeliveryType DeliveryType => DeliveryType.ReliableOrdered;

    /// <summary>The security authority level required to send this packet.</summary>
    protected virtual PacketAuthority Authority => PacketAuthority.Anyone;

    /// <summary>Fired immediately before <see cref="ApplyOptimistically"/> is executed.</summary>
    public event Action<T> BeforePacketAppliedOptimistically;

    /// <summary>Fired immediately after <see cref="ApplyOptimistically"/> is executed.</summary>
    public event Action<T> AfterPacketAppliedOptimistically;

    /// <summary>Fired immediately before <see cref="Apply"/> is executed.</summary>
    public event Action<T> BeforePacketApplied;

    /// <summary>Fired immediately after <see cref="Apply"/> is executed.</summary>
    public event Action<T> AfterPacketApplied;

    protected PacketWarden() => Initialize();

    /// <summary>
    /// Initializes the warden by hooking into network creation/destruction events.
    /// </summary>
    protected virtual void Initialize()
    {
        Network.OnNetworkCreated += RegisterPacket;
        Network.OnNetworkDestroyed += UnregisterPacket;

        if (H.IsInRaid() && Network != null) RegisterPacket(); // Hot reloading
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        Network.OnNetworkCreated -= RegisterPacket;
        Network.OnNetworkDestroyed -= UnregisterPacket;

        if (H.IsInRaid() && Network != null) UnregisterPacket(); // Hot reloading
    }

    /// <summary>
    /// Registers the packet and its corresponding RejectionPacket type with the network backend.
    /// </summary>
    private void RegisterPacket()
    {
        H.Log($"Registering {typeof(T).Name}");

        Network.RegisterPacketWarden<T>(WhenReceivedInternal);
        Network.RegisterPacketWarden<RejectionPacket<T>>(WhenRejectionReceivedInternal);

        IsRegistered = true;
    }

    /// <summary>
    /// Unregisters the packet from the network backend and clears rate limit states.
    /// </summary>
    private void UnregisterPacket()
    {
        try
        {
            _serverRateLimiter.Clear();
            Network.UnregisterPacketWarden<T>();
            Network.UnregisterPacketWarden<RejectionPacket<T>>();
        }
        catch (Exception ex)
        {
            H.Log($"Packet Unregistration Failed: {ex}");
        }
        finally
        {
            IsRegistered = false;
        }
    }

    private void WhenReceivedInternal(T packet, int peerId)
    {
        if (Network.IsServer)
            WhenServerReceivesPacket(packet, peerId);
        else
            WhenClientReceivesPacket(packet, peerId);
    }

    private void WhenRejectionReceivedInternal(RejectionPacket<T> packet, int peerId)
    {
        if (Network.IsClient)
            WhenClientReceivesRejection(packet, peerId);
    }

    /// <summary>
    /// Evaluates whether a peer ID is unauthorized to send this packet based on the <see cref="Authority"/> config.
    /// </summary>
    protected virtual bool IsUnauthorized(int id) => false;

    /// <summary>
    /// The entry point for sending a packet. <br/>
    /// Dispatches the packet to the network.<br/>
    /// Clients send to the server; the server processes and broadcasts.
    /// </summary>
    /// <param name="packet">The packet payload to send.</param>
    /// <param name="targetPeerId">If provided (and invoked by the server), the packet is sent only to this specific peer.</param>
    protected void DispatchPacket(ref T packet, int? targetPeerId = null)
    {
        if (!H.IsInRaid()) return;

#if DEBUG
        // Bypass networking entirely if in the Hideout
        if (H.GameWorld is HideoutGameWorld)
        {
            ApplyOptimisticallyInternal(packet);
            ApplyInternal(packet, INetworkBackend.LocalPeerId);
            return;
        }
#endif

        if (!Network.IsHeadless && IsUnauthorized(H.MainPlayer.Id)) return;

#if DEBUG
        if (ShouldLog) H.Log($"Sending {typeof(T).Name} at {DateTime.UtcNow}");
#endif

        ApplyOptimisticallyInternal(packet);

        if (Network.IsClient)
        {
            Network.SendData(ref packet, DeliveryType, false);
            return;
        }

        // if we are the server and we are not sending the packet to anyone specifically
        if (targetPeerId == null)
        {
            ProcessApprovedPacket(ref packet, INetworkBackend.LocalPeerId);
        }
        else
        {
            MutateApprovedPacket(ref packet, targetPeerId.Value);
            if (targetPeerId.Value != INetworkBackend.LocalPeerId)
            {
                Network.SendDataToPeer(ref packet, DeliveryType, targetPeerId.Value);
            }
            else
            {
                ApplyInternal(packet, INetworkBackend.LocalPeerId);
            }
        }
    }

    /// <summary>
    /// Standard server-side pipeline for incoming packets. <br/>
    /// RateLimit -> Auth Check -> Sanitize -> Validate -> ProcessApprovedPacket
    /// </summary>
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

    /// <summary>
    /// Called after a packet passes all validation and rate limits on the server. <br/>
    /// Mutates the packet, broadcasts it to clients, and applies it locally. <br/>
    /// <b>OVERRIDE THIS METHOD ONLY TO DEFER PACKET APPLICATION OR MODIFY WHO GETS THE INFORMATION <br/>
    /// USE MUTATEAPPROVEDPACKET AND APPLY FOR ALL THE ACTUAL PACKET LOGIC</b>
    /// </summary>
    protected virtual void ProcessApprovedPacket(ref T packet, int peerId)
    {
        MutateApprovedPacket(ref packet, peerId);
        Network.SendData(ref packet, DeliveryType, true); // Broadcast
        ApplyInternal(packet, peerId);
    }

    /// <summary>
    /// Allows the server to alter packet data before broadcasting it (e.g., forcing a server-authoritative Timestamp).
    /// </summary>
    protected virtual void MutateApprovedPacket(ref T packet, int peerId) { }

    /// <summary>
    /// Called when the client receives an approved packet from the server.
    /// </summary>
    protected virtual void WhenClientReceivesPacket(T packet, int peerId)
    {
#if DEBUG
        if (ShouldLog) H.Log($"Receiving {typeof(T).Name} from Server");
#endif
        ApplyInternal(packet, peerId);
    }

    /// <summary>
    /// Wraps the packet execution with Event invocations and NetworkTime bootstrapping.
    /// </summary>
    protected void ApplyInternal(T packet, int peerId)
    {
        TryInvokeAction(BeforePacketApplied, packet);
        OptionalBoostrap(packet);
        Apply(packet, peerId);
        TryInvokeAction(AfterPacketApplied, packet);
    }

    /// <summary>
    /// Wraps the optimistic packet execution with Event invocations.
    /// </summary>
    private void ApplyOptimisticallyInternal(T packet)
    {
        TryInvokeAction(BeforePacketAppliedOptimistically, packet);
        ApplyOptimistically(packet);
        TryInvokeAction(AfterPacketAppliedOptimistically, packet);
    }

    /// <summary>
    /// Bootstraps NetworkTime offsets if the packet implements <see cref="IServerTimestampedPacket"/>.
    /// </summary>
    void OptionalBoostrap(T packet)
    {
        if (!NetworkTime.HasSync && Network.IsClient && packet is IServerTimestampedPacket stamped)
        {
            NetworkTime.BootstrapFromServerStamp(stamped.Timestamp);
        }
    }

    /// <summary>
    /// Handles the event when the server rejects a previously dispatched packet.
    /// </summary>
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
            Notify(rejectedPacket.rejectionReason);
        }

        WhenRejected(rejectedPacket.Payload, peerId);
    }

    protected virtual void Notify(string rejectionReason)
    {
        H.Notify(rejectionReason);
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

    /// <summary>
    /// Sends a <see cref="RejectionPacket{T}"/> back to the offending peer.
    /// </summary>
    protected void SendRejection(ref T packet, int peerId, string rejectionReason = null)
    {
        var rejected = new RejectionPacket<T> { Payload = packet, rejectionReason = rejectionReason };
        Network.SendDataToPeer(ref rejected, DeliveryType, peerId);
    }

    /// <summary>
    /// Invoked when a peer exceeds the ServerRateLimit threshold.
    /// </summary>
    protected virtual void OnRateLimited(T packet, int peerId, in RateLimitConfig config)
    {
        H.Log($"Rate-limiting peer {peerId}, Packet {GetType().Name}");
    }

    /// <summary>
    /// Evaluates the peer against the token bucket rate limiter. <br/>
    /// Returns true if the packet is allowed through.
    /// </summary>
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
                if (canSendReject) SendRejection(ref packet, peerId, "You've been rate limited.");
                return false;
            case RateLimitAction.Disconnect:
                Network.DisconnectPeer(peerId);
                return false;
            default:
                return false;
        }
    }

    /// <summary>
    /// Protects against spoofing. <br/>
    /// Ensures that an <see cref="IAuthoredPacket"/>  was actually generated by the player representing that peer ID.
    /// </summary>
    protected virtual bool SanitizeMetadata(ref T packet, int peerId, out string rejectionReason)
    {
        if (packet is IAuthoredPacket authoredPacket)
        {
            if (authoredPacket.Player == null)
            {
                rejectionReason = "Unknown or null player";
                return false;
            }

            // Verify the sender peerId matches the player object to prevent spoofing
            Player senderPlayer = Network.GetPlayerByPeerId(peerId);
            if (authoredPacket.Player != senderPlayer)
            {
                rejectionReason = "You can't send packets for other players";
                return false;
            }
        }

        rejectionReason = null;
        return true;
    }

    /// <summary>
    /// Allows inheritors to provide custom gameplay validation logic (e.g., checking if the player has enough money).
    /// </summary>
    /// <returns>True if valid, false to abort and send a rejection.</returns>
    protected virtual bool ValidatePacket(T packet, int peerId, out string rejectionReason)
    {
        rejectionReason = string.Empty;
        return true;
    }

    /// <summary>
    /// Called instantly upon dispatching the packet for Client-Side Prediction. <br/>
    /// Mutate local state here before the server confirms it. <br/>
    /// <b>WARNING: This should really only be used for SFX/VFX.</b>
    /// </summary>
    protected virtual void ApplyOptimistically(T packet) { }

    /// <summary>
    /// The core execution logic for this packet type. <br/>
    /// Executed locally on the server immediately, and on clients once received from the server.
    /// </summary>
    /// <param name="packet">The deserialized packet payload.</param>
    /// <param name="peerId">The ID of the peer who sent it.</param>
    protected abstract void Apply(T packet, int peerId);

    /// <summary>
    /// Fallback logic invoked on the client if the server rejects this packet. <br/>
    /// Used to rollback state changes made in <see cref="ApplyOptimistically"/>.
    /// </summary>
    protected virtual void WhenRejected(T packet, int peerId) { }
}