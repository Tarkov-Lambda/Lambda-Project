using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using PacketHandler;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct ReplenishPacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<ReplenishPacket>(reader);
}

public class ReplenishPacketHandler : LambdaPacketHandler<ReplenishPacket>
{
    public void Send() => DispatchPacket(new ReplenishPacket { Player = H.MainPlayer, });

    protected override void Apply(ReplenishPacket packet, NetPeer peer)
    {
        RU.Replenish(packet.Player);
    }
}