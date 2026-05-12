using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using PacketHandler;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct LoadProgressPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public float progress;
}

public class LoadProgressPacketHandler : LambdaPacketHandler<LoadProgressPacket>
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
        packet.Player.GetScore()?.ChangeProgress(packet.progress);
    }
}