using System;

namespace PacketWarden.RateLimiting;

public readonly struct RateLimitConfig
{
    public readonly bool Enabled;
    public readonly double RefillPerSecond;
    public readonly double Burst;
    public readonly int CostPerPacket;
    public readonly RateLimitAction Action;

    // Caps how often a limited peer is rejected and logged.
    public readonly double RejectCooldownSeconds;

    public RateLimitConfig(
        bool enabled,
        double refillPerSecond,
        double burst,
        int costPerPacket = 1,
        RateLimitAction action = RateLimitAction.Reject,
        double rejectCooldownSeconds = 0.25)
    {
        Enabled = enabled;
        RefillPerSecond = Math.Max(0, refillPerSecond);
        Burst = Math.Max(0, burst);
        CostPerPacket = Math.Max(1, costPerPacket);
        Action = action;
        RejectCooldownSeconds = Math.Max(0, rejectCooldownSeconds);
    }
}