using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;

namespace ifp.arena.bep.networking.TimeSync;

[MemoryPackable]
public partial struct TimeSyncResponsePacket : INetSerializable
{
    public int targetPeerId;
    public double clientSendLocalSeconds;
    public double serverSendSeconds;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);

    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<TimeSyncResponsePacket>(reader);
}

public class TimeSyncResponsePacketHandler : PacketHandler<TimeSyncResponsePacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;
    protected override DeliveryMethod DeliveryMethod => DeliveryMethod.Sequenced;

    protected override bool ShouldLog => false;

    protected override void Apply(TimeSyncResponsePacket packet, NetPeer peer)
    {
        if (H.IsServer)
            return;

        double clientReceiveLocal = NetworkTime.LocalNowSeconds;
        NetworkTime.ApplySample(packet.clientSendLocalSeconds, clientReceiveLocal, packet.serverSendSeconds);
    }
}