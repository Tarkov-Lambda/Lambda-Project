using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct TemplatePacket : INetSerializable
{
    public int id;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<TemplatePacket>(reader);
}

public class TemplatePacketHandler : PacketHandler<TemplatePacket>
{
    public void Send(int id)
    {
        var packet = new TemplatePacket
        {
            id = id,
        };

        RequestSend(packet);
    }

    protected override bool PacketValidation(ref TemplatePacket packet, NetPeer netPeer)
    {
        return true;
    }

    protected override void WhenApproved(TemplatePacket packet, NetPeer peer)
    {
        // D.Notify($"{packet}");
    }
}