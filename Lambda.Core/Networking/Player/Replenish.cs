using EFT;
using MemoryPack;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct ReplenishPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }
}

public class ReplenishPacketHandler : LambdaPacketHandler<ReplenishPacket>
{
    public void Send() => DispatchPacket(new ReplenishPacket { Player = H.MainPlayer, });

    protected override void Apply(ReplenishPacket packet, int peerId)
    {
        RU.Replenish(packet.Player);
    }
}