using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;
using PacketHandler.RateLimiting;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct BlindFirePacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public int value; // -1 = side fire, 0 = none, 1 = over-top

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<BlindFirePacket>(reader);
}

public class BlindFirePacketHandler : PacketHandler<BlindFirePacket>
{
    protected override bool ShouldLog => false;

    // protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(2);

    public void Send(int value)
    {
        var packet = new BlindFirePacket
        {
            Player = H.MainPlayer,
            value  = value
        };
        DispatchPacket(packet);
    }

    protected override void Apply(BlindFirePacket packet, NetPeer peer)
    {
        if (packet.Player == null || packet.Player.IsYourPlayer) return;

        packet.Player.HandsController?.BlindFire(packet.value);
    }
}