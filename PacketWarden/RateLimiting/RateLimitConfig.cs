using System;

namespace PacketWarden.RateLimiting;

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
}