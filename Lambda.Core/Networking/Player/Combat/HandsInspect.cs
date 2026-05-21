using EFT;
using MemoryPack;
using static EFT.Player;
using PacketWarden.RateLimiting;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct HandsInspectPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }
}

public class HandsInspectPacketWarden : LambdaPacketWarden<HandsInspectPacket>
{
    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(2);

    protected override bool ShouldLog => false;

    public void Send()
    {
        var packet = new HandsInspectPacket { Player = H.MainPlayer };

        DispatchPacket(ref packet);
    }

    protected override void Apply(HandsInspectPacket packet, int peerId)
    {
        if (packet.Player.IsYourPlayer) return;

        if (packet.Player.HandsController is EmptyHandsController emptyHands)
        {
            emptyHands.ExamineWeapon();
        }
    }
}