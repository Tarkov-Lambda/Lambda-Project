using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core.Gamemode;
using PacketHandler;
using ifp.arena.bep.networking.TimeSync;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct MatchStateSyncPacket : INetSerializable, IServerTimestampedPacket
{
    public double Timestamp { get; set; }  // Phase start time (server clock)
    public double serverNowSeconds;        // Current server time at send — used for NTP bootstrap on late joiners

    public MatchState matchState;
    public RoundActionPhaseEnd? roundActionEnd;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<MatchStateSyncPacket>(reader);
}

public class MatchStateSyncPacketHandler : PacketHandler<MatchStateSyncPacket>
{
    public MatchStateSyncPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

    public void Send(MatchState matchState, double phaseDurationSeconds, RoundActionPhaseEnd? roundActionEnd)
    {
        var packet = new MatchStateSyncPacket
        {
            matchState = matchState,
            Timestamp = NetworkTime.ServerNowSeconds,    // phase start = right now
            serverNowSeconds = NetworkTime.ServerNowSeconds,    // same value; both fields identical for new phase starts
            roundActionEnd = roundActionEnd
        };
        DispatchPacket(packet);
    }

    // Send current phase state to a late/mid-session joiner.
    // Timestamp = ServerPhaseStartSeconds (historical) so the client computes correct remaining time.
    // serverNowSeconds = current server time so the client can bootstrap its NTP offset immediately.
    public void SendToLateJoiner(NetPeer peer)
    {
        var packet = new MatchStateSyncPacket
        {
            matchState = H.Session.matchState,
            Timestamp = H.Arena.ServerPhaseStartSeconds, // historical phase start — preserved by DispatchPacket fix
            serverNowSeconds = NetworkTime.ServerNowSeconds,    // current time — used for NTP bootstrap
            roundActionEnd = H.Arena.PendingRoundActionEnd
        };
        DispatchPacketToPeer(packet, peer);
    }

    protected override bool ValidatePacket(MatchStateSyncPacket packet, NetPeer peer, out string rejectionReason)
    {
        return base.ValidatePacket(packet, peer, out rejectionReason);
    }

    protected override void MutateApprovedPacket(ref MatchStateSyncPacket packet, NetPeer peer)
    {
        packet.Timestamp = NetworkTime.ServerNowSeconds;
    }

    protected override void Apply(MatchStateSyncPacket packet, NetPeer peer)
    {
        H.Arena.TransitionToState(packet);
    }
}