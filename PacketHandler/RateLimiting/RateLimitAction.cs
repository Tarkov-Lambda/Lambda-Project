namespace PacketHandler.RateLimiting;

public enum RateLimitAction
{
    Drop = 0,
    Reject = 1,
    Disconnect = 2
}