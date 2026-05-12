using ifp.arena.bep.Core.Gamemode;
using PacketHandler;
using MemoryPack;
using PacketHandler.TimeSync;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct MatchStateSyncPacket : IPacket, IServerTimestampedPacket
{
    public double Timestamp { get; set; }  // Phase start time (server clock)
    public double serverNowSeconds;        // Current server time at send — used for NTP bootstrap on late joiners

    public MatchState matchState;
    public RoundActionPhaseEnd? roundActionEnd;
}

public class MatchStateSyncPacketHandler : LambdaPacketHandler<MatchStateSyncPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(MatchState matchState, double phaseDurationSeconds, RoundActionPhaseEnd? roundActionEnd)
    {
        var packet = new MatchStateSyncPacket
        {
            matchState = matchState,
            Timestamp = NetworkTime.ServerNowSeconds,           // phase start = right now
            serverNowSeconds = NetworkTime.ServerNowSeconds,    // same value; both fields identical for new phase starts
            roundActionEnd = roundActionEnd
        };
        DispatchPacket(packet);
    }

    // Send current phase state to a late/mid-session joiner.
    // Timestamp = ServerPhaseStartSeconds (historical) so the client computes correct remaining time.
    // serverNowSeconds = current server time so the client can bootstrap its NTP offset immediately.
    public void SendToLateJoiner(int peerId)
    {
        var packet = new MatchStateSyncPacket
        {
            matchState = H.Session.matchState,
            Timestamp = H.Arena.ServerPhaseStartSeconds,        // historical phase start — preserved by DispatchPacket fix
            serverNowSeconds = NetworkTime.ServerNowSeconds,    // current time — used for NTP bootstrap
            roundActionEnd = H.Arena.PendingRoundActionEnd
        };
        DispatchPacketToPeer(packet, peerId);
    }

    protected override bool ValidatePacket(MatchStateSyncPacket packet, int peerId, out string rejectionReason)
    {
        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }

    protected override void Apply(MatchStateSyncPacket packet, int peerId)
    {
        H.Arena.TransitionToState(packet);
    }
}