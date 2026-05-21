using System.Text.RegularExpressions;
using EFT;
using Fika.Core.Networking.Pooling;
using MemoryPack;
using PacketWarden.TimeSync;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct PausePacket : IPacket, IAuthoredPacket, IServerTimestampedPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public double Timestamp { get; set; }
}

public class SessionPausePacketWarden : LambdaPacketWarden<PausePacket>
{
    public void Send()
    {
        var packet = new PausePacket
        {
            Timestamp = NetworkTime.ServerNowSeconds
        };
        DispatchPacket(ref packet);
    }

    protected override bool ValidatePacket(PausePacket packet, int peerId, out string rejectionReason)
    {
        rejectionReason = null;
        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }

    protected override void MutateApprovedPacket(ref PausePacket packet, int peerId)
    {
        packet.Timestamp = NetworkTime.ServerNowSeconds;
    }

    protected override void Apply(PausePacket packet, int peerId)
    {
        var matchStateSyncPacket = new MatchStateSyncPacket
        {
            matchState = MatchState.Pause,
            Timestamp = packet.Timestamp,
        };
        H.Arena.TransitionToState(matchStateSyncPacket);
    }
}