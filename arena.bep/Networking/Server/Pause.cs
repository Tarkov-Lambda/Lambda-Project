using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using PacketHandler;
using ifp.arena.shared;
using MemoryPack;
using Fika.Core.Networking.Snapshotting;
using PacketHandler.TimeSync;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PausePacket : IPacket, IAuthoredPacket, IServerTimestampedPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public double Timestamp { get; set; }
}

public class PausePacketHandler : LambdaPacketHandler<PausePacket>
{
    public void Send()
    {
        var packet = new PausePacket { Timestamp = NetworkTime.ServerNowSeconds };
        DispatchPacket(packet);
    }

    protected override bool ValidatePacket(PausePacket packet, int peerId, out string rejectionReason)
    {
        rejectionReason = null;
        return H.Session.matchState == MatchState.RoundPrepare;
    }

    protected override void MutateApprovedPacket(ref PausePacket packet, int peerId)
    {
        packet.Timestamp = NetworkTime.ServerNowSeconds;
    }

    protected override void Apply(PausePacket packet, int peerId)
    {
        MatchStateSyncPacket matchStateSyncPacket = new MatchStateSyncPacket
        {
            matchState = MatchState.Pause,
            Timestamp = packet.Timestamp,
        };
        H.Arena.TransitionToState(matchStateSyncPacket);
    }
}