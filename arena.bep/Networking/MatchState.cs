using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using PacketHandler;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct MatchStateSyncPacket : INetSerializable, IServerTimestampedPacket
{
    public double Timestamp {get; set;}

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
            Timestamp = NetworkTime.ServerNowSeconds,
            roundActionEnd = roundActionEnd
        };
        DispatchPacket(packet);
    }

    // Send current phase state to a late/mid-session joiner.
    // We send ServerPhaseStartSeconds (not Now) so the client recomputes the correct remaining time.
    public void SendToPlayer(Player player)
    {
        var packet = new MatchStateSyncPacket
        {
            matchState = H.Session.matchState,
            Timestamp = H.Arena.ServerPhaseStartSeconds,
            roundActionEnd = H.Arena.PendingRoundActionEnd
        };
        RequestSendToPlayer(packet, player.Id);
    }

    protected override void WhenApproved(MatchStateSyncPacket packet, NetPeer peer)
    {
        H.Arena.TransitionToState(packet);
    }
}