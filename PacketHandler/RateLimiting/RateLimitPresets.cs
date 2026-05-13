namespace PacketHandler.RateLimiting;

public static class RateLimitPresets
{
    public static RateLimitConfig Disabled => new(enabled: false, 0, 0);

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


    public static RateLimitConfig HighFrequency => new(
        enabled: true,
        refillPerSecond: 60,
        burst: 120,
        action: RateLimitAction.Drop);

    public static RateLimitConfig StrictInteraction => new(
        enabled: true,
        refillPerSecond: 2,
        burst: 5,
        action: RateLimitAction.Reject);

    public static RateLimitConfig SecurityCritical => new(
        enabled: true,
        refillPerSecond: 1,
        burst: 3,
        action: RateLimitAction.Disconnect);


    public static RateLimitConfig LimitPerSecond(double maxPerSecond, RateLimitAction action = RateLimitAction.Reject)
    {
        return new RateLimitConfig(
            enabled: true,
            refillPerSecond: maxPerSecond,
            burst: maxPerSecond * 2,
            costPerPacket: 1,
            action: action
        );
    }

    public static RateLimitConfig LimitByCooldown(double cooldownSeconds, RateLimitAction action = RateLimitAction.Reject)
    {
        return new RateLimitConfig(
            enabled: true,
            refillPerSecond: 1.0 / cooldownSeconds,
            burst: 1,
            costPerPacket: 1,
            action: action
        );
    }
}