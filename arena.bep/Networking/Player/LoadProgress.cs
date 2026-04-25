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
public partial struct LoadProgressPacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public float progress;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<LoadProgressPacket>(reader);
}

public class LoadProgressPacketHandler : PacketHandler<LoadProgressPacket>
{
    public void Send(float progress)
    {
        if (H.IsHeadless) return;

        var packet = new LoadProgressPacket
        {
            Player = H.MainPlayer,
            progress = progress
        };

        DispatchPacket(packet);
    }

    protected override void Apply(LoadProgressPacket packet, NetPeer peer)
    {
        packet.Player.GetScore()?.ChangeProgress(packet.progress);
    }
}