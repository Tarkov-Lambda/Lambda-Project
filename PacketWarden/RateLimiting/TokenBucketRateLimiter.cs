using System;
using System.Collections.Generic;

namespace PacketWarden.RateLimiting;

// Not thread-safe; intended to be used on the network thread.
public sealed class TokenBucketRateLimiter<TKey> where TKey : notnull
{
    private struct Bucket
    {
        public double Tokens;
        public double LastRefillSeconds;
        public double LastSeenSeconds;
        public double LastRejectSentSeconds;
    }

    private readonly Dictionary<TKey, Bucket> _buckets = new();

    public void Clear() => _buckets.Clear();

    public bool TryConsume(TKey key, double nowSeconds, in RateLimitConfig config, out bool canSendReject)
    {
        canSendReject = false;

        if (!config.Enabled)
            return true;

        double refill = Math.Max(0, config.RefillPerSecond);
        double burst = Math.Max(0, config.Burst);
        int cost = Math.Max(1, config.CostPerPacket);

        if (!_buckets.TryGetValue(key, out var b))
        {
            b = new Bucket
            {
                Tokens = burst,
                LastRefillSeconds = nowSeconds,
                LastSeenSeconds = nowSeconds,
                LastRejectSentSeconds = double.NegativeInfinity
            };
        }

        // Refill
        var elapsed = Math.Max(0, nowSeconds - b.LastRefillSeconds);
        if (elapsed > 0 && refill > 0)
        {
            b.Tokens = Math.Min(burst, b.Tokens + elapsed * refill);
            b.LastRefillSeconds = nowSeconds;
        }

        b.LastSeenSeconds = nowSeconds;

        if (b.Tokens >= cost)
        {
            b.Tokens -= cost;
            _buckets[key] = b;
            return true;
        }

        // Rate-limited.
        if (config.Action == RateLimitAction.Reject)
        {
            canSendReject = (nowSeconds - b.LastRejectSentSeconds) >= config.RejectCooldownSeconds;
            if (canSendReject)
                b.LastRejectSentSeconds = nowSeconds;
        }

        _buckets[key] = b;
        return false;
    }

    public void Prune(double nowSeconds, double ttlSeconds)
    {
        if (_buckets.Count == 0)
            return;

        ttlSeconds = Math.Max(0, ttlSeconds);
        if (ttlSeconds <= 0)
            return;

        // Avoid allocations by doing a single pass with a temp list only when needed.
        List<TKey> toRemove = null;
        foreach (var kvp in _buckets)
        {
            if ((nowSeconds - kvp.Value.LastSeenSeconds) > ttlSeconds)
            {
                toRemove ??= new List<TKey>();
                toRemove.Add(kvp.Key);
            }
        }

        if (toRemove == null)
            return;

        foreach (var key in toRemove)
            _buckets.Remove(key);
    }
}