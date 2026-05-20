using EFT;
using MemoryPack;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct BlindFirePacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public int value; // -1 = side fire, 0 = none, 1 = over-top
}

public class BlindFirePacketWarden : LambdaPacketWarden<BlindFirePacket>
{
    protected override bool ShouldLog => false;

    public void Send(int value)
    {
        var packet = new BlindFirePacket
        {
            Player = H.MainPlayer,
            value  = value
        };
        DispatchPacket(packet);
    }

    protected override void Apply(BlindFirePacket packet, int peerId)
    {
        if (packet.Player == null || packet.Player.IsYourPlayer) return;

        packet.Player.HandsController?.BlindFire(packet.value);
    }
}