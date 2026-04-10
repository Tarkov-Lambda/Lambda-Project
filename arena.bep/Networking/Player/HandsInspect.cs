using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking.Base;
using MemoryPack;
using static EFT.Player;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct HandsInspectPacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player player { get; set; }

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<HandsInspectPacket>(reader);
}

public class HandsInspectPacketHandler : PacketHandler<HandsInspectPacket>
{
    public void Send() => RequestSend(new HandsInspectPacket { });

    protected override void WhenApproved(HandsInspectPacket packet, NetPeer peer)
    {
        if (packet.player.IsYourPlayer) return;

        if (packet.player.HandsController is EmptyHandsController emptyHands)
        {
            emptyHands.ExamineWeapon();
        }
    }
}