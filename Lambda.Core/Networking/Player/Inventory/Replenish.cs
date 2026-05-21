using EFT;
using MemoryPack;

[MemoryPackable]
public partial struct ReplenishPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }
}

public class ReplenishPacketWarden : LambdaPacketWarden<ReplenishPacket>
{
    public void Send()
    {
        var packet = new ReplenishPacket { Player = H.MainPlayer };
        DispatchPacket(ref packet);
    }

    protected override void Apply(ReplenishPacket packet, int peerId)
    {
        RU.Replenish(packet.Player);
    }
}