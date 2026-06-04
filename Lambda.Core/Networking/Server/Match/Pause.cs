using System.Text.RegularExpressions;
using EFT;
using Fika.Core.Networking.Pooling;
using MemoryPack;
using PacketWarden.TimeSync;

namespace Lambda.Core.Networking;

public enum PausePacketRequestType : byte
{
    Pause,
    Unpause
}

[MemoryPackable]
public partial struct PausePacket : IPacket, IAuthoredPacket, IServerTimestampedPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public double Timestamp { get; set; }

    public PausePacketRequestType type;
}

// A little crude but it will work
public class SessionPausePacketWarden : LambdaPacketWarden<PausePacket>
{
    protected override bool ShouldDisplayRejectionInChat => true;

    public void Pause()
    {
        var packet = new PausePacket
        {
            type = PausePacketRequestType.Pause
        };
        DispatchPacket(ref packet);
    }

    public void Unpause()
    {
        var packet = new PausePacket
        {
            type = PausePacketRequestType.Unpause
        };
        DispatchPacket(ref packet);
    }

    public void RequestUnpause()
    {

    }

    protected override bool ValidatePacket(PausePacket packet, int peerId, out string rejectionReason)
    {
        if (packet.type is PausePacketRequestType.Pause)
        {
            if (H.Session.matchState is not MatchState.RoundPrepare)
            {
                rejectionReason = "Pause can only be requested during preparation phase.";
                return false;
            }
        }
        else
        {
            if (!packet.Player.Context.IsAdmin)
            {
                if (H.Session.matchState is not MatchState.RoundPrepare)
                {
                    rejectionReason = "Unpause request is only valid during preparation phase.";
                    return false;
                }

                var elapsed = NetworkTime.ServerNowSeconds - H.Arena.ServerPhaseStartSeconds;

                if (elapsed < 15f)
                {
                    rejectionReason = $"Unpause not allowed yet ({elapsed:0.0}s / 15s required).";
                    return false;
                }
            }
        }

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