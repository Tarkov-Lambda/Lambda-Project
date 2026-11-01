using System;
using System.Collections.Generic;

namespace PacketWarden.RateLimiting;

public sealed class TokenBucketRateLimiter<TKey>
    where TKey : notnull
{
    private struct Bucket
    {
        public double Tokens;
        public double LastRefillSeconds;
        public double LastNotifySeconds;
    }

    private readonly Dictionary<TKey, Bucket> _buckets = new();

    public void Clear() => _buckets.Clear();

    public void Remove(TKey key) => _buckets.Remove(key);

    // notify: the key was limited and has not been told for RejectCooldownSeconds
    public bool TryConsume(TKey key, double nowSeconds, in RateLimitConfig config, out bool notify)
    {
        notify = false;

        if (!config.Enabled)
            return true;

        if (!_buckets.TryGetValue(key, out Bucket bucket))
        {
            bucket = new Bucket
            {
                Tokens = config.Burst,
                LastRefillSeconds = nowSeconds,
                LastNotifySeconds = double.NegativeInfinity,
            };
        }

        bucket.Tokens = Math.Min(
            config.Burst,
            bucket.Tokens
                + Math.Max(0, nowSeconds - bucket.LastRefillSeconds) * config.RefillPerSecond
        );
        bucket.LastRefillSeconds = nowSeconds;

        bool allowed = bucket.Tokens >= config.CostPerPacket;

        if (allowed)
        {
            bucket.Tokens -= config.CostPerPacket;
        }
        else if (nowSeconds - bucket.LastNotifySeconds >= config.RejectCooldownSeconds)
        {
            bucket.LastNotifySeconds = nowSeconds;
            notify = true;
        }

        _buckets[key] = bucket;
        return allowed;
    }
}
