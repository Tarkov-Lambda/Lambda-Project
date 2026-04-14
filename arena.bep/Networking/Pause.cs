using EFT;
using Fika.Core.Main.Utils;
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
public partial struct PausePacket : INetSerializable, IAuthoredPacket, IServerTimestampedPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public double Timestamp { get; set; }

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<PausePacket>(reader);
}

public class PausePacketHandler : PacketHandler<PausePacket>
{
    public void Send()
    {
        var packet = new PausePacket { Timestamp = NetworkTime.ServerNowSeconds };
        DispatchPacket(packet);
    }

    protected override bool PacketValidation(ref PausePacket packet, NetPeer peer, out string rejectionReason)
    {
        packet.Timestamp = NetworkTime.ServerNowSeconds;
        rejectionReason = null;
        return H.Session.matchState == MatchState.RoundPrepare;
    }

    protected override void WhenApproved(PausePacket packet, NetPeer peer)
    {
        MatchStateSyncPacket matchStateSyncPacket = new MatchStateSyncPacket
        {
            matchState = MatchState.Pause,
            Timestamp = packet.Timestamp,
        };
        H.Arena.TransitionToState(matchStateSyncPacket);
    }
}