using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking.Base;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct BlindFirePacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player player { get; set; }

    public int value; // -1 = side fire, 0 = none, 1 = over-top

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<BlindFirePacket>(reader);
}

public class BlindFirePacketHandler : PacketHandler<BlindFirePacket>
{
    public void Send(int value)
    {
        var packet = new BlindFirePacket { value = value };
        RequestSend(packet);
    }

    protected override void WhenApproved(BlindFirePacket packet, NetPeer peer)
    {
        if (packet.player == null || packet.player.IsYourPlayer) return;

        packet.player.HandsController?.BlindFire(packet.value);
    }
}