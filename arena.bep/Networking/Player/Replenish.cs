using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct ReplenishPacket : INetSerializable
{
    public int id;

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<ReplenishPacket>(reader);
}

public class ReplenishPacketHandler : PacketHandler<ReplenishPacket>
{
    public void Send()
    {
        var packet = new ReplenishPacket
        {
            id = H.MainPlayer.Id,
        };

        RequestSend(packet);
    }

    protected override void WhenApproved(ReplenishPacket packet, NetPeer peer)
    {
        RU.Replenish(H.GetPlayer(packet.id));
    }
}