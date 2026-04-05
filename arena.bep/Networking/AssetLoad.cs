using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking.Base;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct AssetLoadStatePacket : INetSerializable
{
    public int id;
    public bool isReady;
    public string msg;

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<AssetLoadStatePacket>(reader);
}

public class AssetLoadStatePacketHandler : PacketHandler<AssetLoadStatePacket>
{
    public void Send(bool isLoaded, string msg)
    {
        var packet = new AssetLoadStatePacket
        {
            id = H.MainPlayer.Id,
            isReady = isLoaded,
            msg = msg
        };

        RequestSend(packet);
    }

    protected override void WhenApproved(AssetLoadStatePacket packet, NetPeer peer)
    {
        H.GetPlayerScore(packet.id)?.isMapReady = packet.isReady;
    }
}