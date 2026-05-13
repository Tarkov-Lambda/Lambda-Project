using PacketHandler.RateLimiting;
using MemoryPack;
using System;

namespace PacketHandler.TimeSync;

[MemoryPackable]
public partial struct TimeSynchronizationPacket : IPacket
{
    public double clientSendLocalSeconds;
}

public class TimeSynchronizationPacketHandler : PacketHandler<TimeSynchronizationPacket>
{
    protected override bool ShouldLog => false;

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitPerSecond(3, RateLimitAction.Drop);
    protected override DeliveryType DeliveryType => DeliveryType.Sequenced;

    public void Send()
    {
        if (Plugin.Network.IsServer)
            return;

        var packet = new TimeSynchronizationPacket
        {
            clientSendLocalSeconds = NetworkTime.LocalNowSeconds
        };

        try
        {
            DispatchPacket(packet);
        }
        catch (NullReferenceException) { } // idrc
    }

    protected override void ProcessApprovedPacket(ref TimeSynchronizationPacket packet, int peerId)
    {
        ApplyInternal(packet, peerId);
    }

    protected override void Apply(TimeSynchronizationPacket packet, int peerId)
    {
        if (H.GameWorld is HideoutGameWorld) return;

        var response = new TimeSyncResponsePacket
        {
            targetPeerId = peerId,
            clientSendLocalSeconds = packet.clientSendLocalSeconds,
            serverSendSeconds = NetworkTime.LocalNowSeconds
        };

        Plugin.Network.SendDataToPeer(ref response, DeliveryType.ReliableOrdered, peerId);
    }
}