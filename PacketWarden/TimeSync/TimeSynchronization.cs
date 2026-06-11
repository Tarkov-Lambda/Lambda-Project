using PacketWarden.RateLimiting;
using MemoryPack;
using System;

namespace PacketWarden.TimeSync;

[MemoryPackable]
public partial struct TimeSynchronizationPacket : IPacket
{
    public double clientSendLocalSeconds;
    public double serverSendSeconds;
}

public class TimeSynchronizationPacketWarden : PacketWarden<TimeSynchronizationPacket>
{
    protected override bool ShouldLog => false;

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitPerSecond(2, RateLimitAction.Drop);
    protected override DeliveryType DeliveryType => DeliveryType.Sequenced;

    public void Send()
    {
        var packet = new TimeSynchronizationPacket
        {
            clientSendLocalSeconds = NetworkTime.LocalNowSeconds
        };

        try
        {
            DispatchPacket(ref packet);
        }
        catch (NullReferenceException) { } // idrc
    }

    protected override void ProcessApprovedPacket(ref TimeSynchronizationPacket packet, int peerId)
    {
        packet.serverSendSeconds = NetworkTime.LocalNowSeconds;
        Network.SendDataToPeer(ref packet, DeliveryType, peerId);
    }

    protected override void Apply(TimeSynchronizationPacket packet, int peerId)
    {
        NetworkTime.ApplySample(packet.clientSendLocalSeconds, NetworkTime.LocalNowSeconds, packet.serverSendSeconds);
    }
}