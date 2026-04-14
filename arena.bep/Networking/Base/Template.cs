using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;

namespace ifp.arena.bep.networking;

// if you wanna automatically de/serialize
// if you need to create a custom MemoryPack class formatter - look at PlayerFormatter (don't forget to register it like I do in Plugin)
[MemoryPackable]
public partial struct TemplatePacket : INetSerializable
{
    public int id;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<TemplatePacket>(reader);
}

// if you wanna manually de/serialize
public partial struct ManuallySerializedTemplatePacket : INetSerializable
{
    public int id;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(id);
    }

    public void Deserialize(NetDataReader reader)
    {
        id = reader.GetInt();
    }
}

public class TemplatePacketHandler : PacketHandler<TemplatePacket>
{
    public void Send(int id)
    {
        var packet = new TemplatePacket
        {
            id = id,
        };

        DispatchPacket(packet);
    }

    protected override bool PacketValidation(ref TemplatePacket packet, NetPeer peer, out string rejectionReason)
    {
        rejectionReason = null;
        return true;
    }

    protected override void WhenApproved(TemplatePacket packet, NetPeer peer)
    {
        // D.Notify($"{packet}");
    }
}