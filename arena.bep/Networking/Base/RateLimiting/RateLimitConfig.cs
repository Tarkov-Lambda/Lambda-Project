using System;

namespace ifp.arena.bep.networking.Base.RateLimiting;

public readonly struct RateLimitConfig
{
    public readonly bool Enabled;
    public readonly double RefillPerSecond;
    public readonly double Burst;
    public readonly int CostPerPacket;
    public readonly RateLimitAction Action;
    public readonly double StateTtlSeconds;

    // If server rejects, this caps how often we send rejection packets per peer.
    public readonly double RejectCooldownSeconds;

    public RateLimitConfig(
        bool enabled,
        double refillPerSecond,
        double burst,
        int costPerPacket = 1,
        RateLimitAction action = RateLimitAction.Reject,
        double stateTtlSeconds = 30,
        double rejectCooldownSeconds = 0.25)
    {
        Enabled = enabled;
        RefillPerSecond = refillPerSecond;
        Burst = burst;
        CostPerPacket = Math.Max(1, costPerPacket);
        Action = action;
        StateTtlSeconds = Math.Max(1, stateTtlSeconds);
        RejectCooldownSeconds = Math.Max(0, rejectCooldownSeconds);
    }

    public static RateLimitConfig Disabled => new(
        enabled: false,
        refillPerSecond: 0,
        burst: 0,
        costPerPacket: 1,
        action: RateLimitAction.Drop);

    // Default policy: 20/s sustained, burst 40, cost 1, reject by default.
    // This is giga relaxed
    public static RateLimitConfig Default => new(
        enabled: true,
        refillPerSecond: 20,
        burst: 40,
        costPerPacket: 1,
        action: RateLimitAction.Reject,
        stateTtlSeconds: 30,
        rejectCooldownSeconds: 0.25);
}