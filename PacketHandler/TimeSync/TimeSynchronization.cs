using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using PacketHandler.RateLimiting;
using MemoryPack;

namespace ifp.arena.bep.networking.TimeSync;

[MemoryPackable]
public partial struct TimeSynchronizationPacket : INetSerializable
{
    public double clientSendLocalSeconds;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<TimeSynchronizationPacket>(reader);
}

public class TimeSynchronizationPacketHandler : PacketHandler<TimeSynchronizationPacket>
{
    protected override bool ShouldLog => false;

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitPerSecond(3, RateLimitAction.Drop);
    protected override DeliveryMethod DeliveryMethod => DeliveryMethod.Sequenced;

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

    protected override void ProcessApprovedPacket(ref TimeSynchronizationPacket packet, NetPeer peer)
    {
        ApplyInternal(packet, peer);
    }

    protected override void Apply(TimeSynchronizationPacket packet, NetPeer peer)
    {
        if (H.GameWorld is HideoutGameWorld) return;

        var response = new TimeSyncResponsePacket
        {
            targetPeerId = peer.Id,
            clientSendLocalSeconds = packet.clientSendLocalSeconds,
            serverSendSeconds = NetworkTime.LocalNowSeconds
        };

        H.FikaNet.SendDataToPeer(ref response, DeliveryMethod.ReliableOrdered, peer);
    }
}