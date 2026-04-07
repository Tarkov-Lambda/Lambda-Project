using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PausePacket : INetSerializable, AuthoredPacket, ServerTimestampedPacket
{
    [MemoryPackAllowSerialize]
    public Player player { get; set; }

    public double timestamp { get; set; }

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<PausePacket>(reader);
}

public class PausePacketHandler : PacketHandler<PausePacket>
{
    public PausePacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.Both) { }

    public void Send()
    {
        var packet = new PausePacket { };

        if (FikaBackendUtils.IsServer)
            packet.timestamp = NetworkTime.ServerNowSeconds;

        RequestSend(packet);
    }

    protected override bool ServerValidation(ref PausePacket packet, NetPeer netPeer)
    {
        packet.timestamp = NetworkTime.ServerNowSeconds;

        if (H.Session.matchState == MatchState.RoundPrepare)
        {
            return true;
        }

        else return false;
    }

    protected override void WhenApproved(PausePacket packet, NetPeer peer)
    {
        MatchStateSyncPacket matchStateSyncPacket = new MatchStateSyncPacket
        {
            matchState = MatchState.Pause,
            timestamp = packet.timestamp,
        };
        H.Arena.TransitionToState(matchStateSyncPacket);
    }
}