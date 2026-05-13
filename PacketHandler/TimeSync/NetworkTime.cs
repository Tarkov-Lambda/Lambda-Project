using System.Diagnostics;

namespace PacketHandler.TimeSync;

public static class NetworkTime
{
    private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    // Smoothed offset (serverTime - localTime)
    private static double _offsetSeconds;
    private static bool _hasSync;
    private static double _estimatedRttSeconds;

    // EMA smoothing for offset updates
    // Higher alpha tracks jitter more aggressively. Lower alpha is steadier
    private const double OffsetEmaAlpha = 0.10;

    // A monotonic local clock.
    public static double LocalNowSeconds => _stopwatch.Elapsed.TotalSeconds;

    // Server: LocalNowSeconds
    // Client: LocalNowSeconds + offset
    public static double ServerNowSeconds => LocalNowSeconds + _offsetSeconds;

    public static bool HasSync => _hasSync;
    public static double OffsetSeconds => _offsetSeconds;
    public static double EstimatedRttSeconds => _estimatedRttSeconds;

    // call on game start / disconnect / reconnect
    public static void Reset()
    {
        _offsetSeconds = 0;
        _hasSync = false;
        _estimatedRttSeconds = 0;
    }

    // Apply an NTP-style time sample
    // t0: client local time when request was sent
    // t3: client local time when response was received
    // tS: server time when response was sent
    // offset sample: tS - (t0 + t3)/2
    // rtt: t3 - t0
    internal static void ApplySample(double clientSendLocalSeconds, double clientReceiveLocalSeconds, double serverSendSeconds)
    {
        if (clientReceiveLocalSeconds < clientSendLocalSeconds)
            return;

        double rtt = clientReceiveLocalSeconds - clientSendLocalSeconds;
        if (rtt < 0)
            return;

        double offsetSample = serverSendSeconds - ((clientSendLocalSeconds + clientReceiveLocalSeconds) * 0.5);

        if (!_hasSync)
        {
            _offsetSeconds = offsetSample;
            _hasSync = true;
        }
        else
        {
            _offsetSeconds = Lerp(_offsetSeconds, offsetSample, OffsetEmaAlpha);
        }

        _estimatedRttSeconds = _estimatedRttSeconds <= 0 ? rtt : Lerp(_estimatedRttSeconds, rtt, 0.10);
    }

    // If we receive an authoritative server timestamp before periodic sync converges
    // we can bootstrap offset from that stamp. This is less accurate than ApplySample (no RTT compensation)
    // but provides a sane starting point
    public static void BootstrapFromServerStamp(double serverStampSeconds)
    {
        double local = LocalNowSeconds;
        double offsetSample = serverStampSeconds - local;

        if (!_hasSync)
        {
            _offsetSeconds = offsetSample;
            _hasSync = true;
        }
    }

    private static double Lerp(double a, double b, double t)
    {
        if (t <= 0) return a;
        if (t >= 1) return b;
        return a + (b - a) * t;
    }
}