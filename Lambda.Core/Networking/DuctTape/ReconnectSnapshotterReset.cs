using System;
using EFT;
using Fika.Core.Main.Players;
using MemoryPack;
using PacketWarden;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct ReconnectSnapshotterResetPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }
}

// there is 100% some bug I've created that causes the server to have some kind of a dissonance in the player snapshotter timestamps
// as a result we just force reset it between rounds because it doesn't seem to cause any harm anyways
public class ReconnectSnapshotterResetPacketWarden : LambdaPacketWarden<ReconnectSnapshotterResetPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(Player player)
    {
        var packet = new ReconnectSnapshotterResetPacket
        {
            Player = player
        };
        DispatchPacket(ref packet);
    }

    protected override void Apply(ReconnectSnapshotterResetPacket packet, int peerId)
    {
        try
        {
            if (packet.Player is ObservedPlayer observedPlayer)
            {
                observedPlayer.ResetSnapshotter();
            }
        }
        catch (Exception) { }

    }
}