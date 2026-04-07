using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct ReplenishPacket : INetSerializable, AuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player player { get; set; }

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<ReplenishPacket>(reader);
}

public class ReplenishPacketHandler : PacketHandler<ReplenishPacket>
{
    public void Send() => RequestSend(new ReplenishPacket { });

    protected override void WhenApproved(ReplenishPacket packet, NetPeer peer)
    {
        RU.Replenish(packet.player);
    }
}