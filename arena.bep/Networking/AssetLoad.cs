using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking.Base;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PlayerReadinessPacket : INetSerializable
{
    public int id;
    public bool isMapReady;
    public int progress;

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<PlayerReadinessPacket>(reader);
}

public class PlayerReadinessPacketHandler : PacketHandler<PlayerReadinessPacket>
{
    public void Send(bool isMapReady, int progress = 0)
    {
        var packet = new PlayerReadinessPacket
        {
            id = H.MainPlayer.Id,
            isMapReady = isMapReady,
            progress = progress
        };

        RequestSend(packet);
    }

    protected override void WhenApproved(PlayerReadinessPacket packet, NetPeer peer)
    {
        H.GetPlayerScore(packet.id)?.isMapReady = packet.isMapReady;
    }
}