using EFT;
using MemoryPack;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct LoadProgressPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public float progress;
}

public class LoadProgressPacketWarden : LambdaPacketWarden<LoadProgressPacket>
{
    public void Send(float progress)
    {
        if (H.IsHeadless) return;

        var packet = new LoadProgressPacket
        {
            Player = H.MainPlayer,
            progress = progress
        };

        // DispatchPacket(packet);
    }

    protected override void Apply(LoadProgressPacket packet, int peerId)
    {
        packet.Player.GetContext()?.ChangeProgress(packet.progress);
    }
}