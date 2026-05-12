using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using PacketHandler.RateLimiting;
using MemoryPack;

namespace PacketHandler.TimeSync;

[MemoryPackable]
public partial struct TimeSynchronizationPacket : IPacket
{
    public double clientSendLocalSeconds;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<TimeSynchronizationPacket>(reader);
}

public class TimeSynchronizationPacketHandler : PacketHandler<TimeSynchronizationPacket>
{
    protected override bool ShouldLog => false;

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitPerSecond(3, RateLimitAction.Drop);
    protected override DeliveryType DeliveryType => DeliveryType.Sequenced;

    public void Send()
    {
        if (H.IsServer)
            return;

        var packet = new TimeSynchronizationPacket
        {
            clientSendLocalSeconds = NetworkTime.LocalNowSeconds
        };

        DispatchPacket(packet);
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