using MemoryPack;

namespace PacketHandler.TimeSync;

[MemoryPackable]
public partial struct TimeSyncResponsePacket : IPacket
{
    public int targetPeerId;
    public double clientSendLocalSeconds;
    public double serverSendSeconds;
}

public class TimeSyncResponsePacketHandler : PacketHandler<TimeSyncResponsePacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;
    protected override DeliveryType DeliveryType => DeliveryType.Sequenced;

    protected override bool ShouldLog => false;

    protected override void Apply(TimeSyncResponsePacket packet, int peerId)
    {
        if (Plugin.Network.IsServer)
            return;

        double clientReceiveLocal = NetworkTime.LocalNowSeconds;
        NetworkTime.ApplySample(packet.clientSendLocalSeconds, clientReceiveLocal, packet.serverSendSeconds);
    }
}