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
    public double timestamp {get; set;}

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
            timestamp = NetworkTime.ServerNowSeconds,
            roundActionEnd = roundActionEnd
        };
        RequestSend(packet);
    }

    protected override void WhenApproved(MatchStateSyncPacket packet, NetPeer peer)
    {
        H.Arena.TransitionToState(packet);
    }
}