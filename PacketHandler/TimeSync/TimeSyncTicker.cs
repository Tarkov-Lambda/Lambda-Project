using Comfort.Common;

namespace PacketHandler.TimeSync;

public class TimeSyncTicker
{
    private double _nextSendLocalSeconds;

    private const double IntervalSeconds = 1.0;

    public TimeSyncTicker()
    {
        _nextSendLocalSeconds = NetworkTime.LocalNowSeconds;
    }

    public void Update()
    {
        if (Plugin.Network.IsServer)
            return;

        double now = NetworkTime.LocalNowSeconds;
        if (now < _nextSendLocalSeconds)
            return;

        _nextSendLocalSeconds = now + IntervalSeconds;

        if (!Singleton<TimeSynchronizationPacketHandler>.Instance.IsRegistered)
            return;

        Singleton<TimeSynchronizationPacketHandler>.Instance.Send();
    }
}