using Comfort.Common;
using System;

namespace PacketHandler.TimeSync;

public class TimeSyncTicker : IDisposable
{
    private double _nextSendLocalSeconds;

    private const double IntervalSeconds = 1.0;

    public TimeSyncTicker()
    {
        _nextSendLocalSeconds = NetworkTime.LocalNowSeconds;
    }

    public void Update()
    {
        if (H.IsServer)
            return;

        double now = NetworkTime.LocalNowSeconds;
        if (now < _nextSendLocalSeconds)
            return;

        _nextSendLocalSeconds = now + IntervalSeconds;

        if (!Singleton<TimeSynchronizationPacketHandler>.Instantiated)
            return;

        if (H.GameWorld == null)
            return;

        Singleton<TimeSynchronizationPacketHandler>.Instance.Send();
    }

    public void Dispose()
    {
        NetworkTime.Reset();
    }
}