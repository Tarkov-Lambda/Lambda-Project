using PacketHandler;
using MemoryPack;

[MemoryPackable]
public partial struct TemplatePacket : IPacket
{
    public int id;
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

    protected override bool ValidatePacket(TemplatePacket packet, int peerId, out string rejectionReason)
    {
        return base.ValidatePacket(packet, peerId, out rejectionReason);

    }

    protected override void Apply(TemplatePacket packet, int peerId)
    {
        // D.Notify($"{packet}");
    }
}