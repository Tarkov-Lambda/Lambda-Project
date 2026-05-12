using PacketHandler.RateLimiting;
using MemoryPack;

namespace PacketHandler.TimeSync;

[MemoryPackable]
public partial struct TestPacket : IPacket
{
    public double clientSendLocalSeconds;
}

public class TestPacketHandler : PacketHandler<TestPacket>
{
    protected override bool ShouldLog => false;

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitPerSecond(3, RateLimitAction.Drop);
    protected override DeliveryType DeliveryType => DeliveryType.Sequenced;

    public void Send()
    {
        H.Log("INSIDE Send TestPacketHandler");

        var packet = new TestPacket
        {
            clientSendLocalSeconds = NetworkTime.LocalNowSeconds
        };

        DispatchPacket(packet);
    }

    protected override void ProcessApprovedPacket(ref TestPacket packet, int peerId)
    {
        ApplyInternal(packet, peerId);
    }

    protected override void Apply(TestPacket packet, int peerId)
    {
        H.Log("INSIDE Apply TestPacketHandler");
    }
}