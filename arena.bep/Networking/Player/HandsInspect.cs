using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;
using static EFT.Player;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct HandsInspectPacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<HandsInspectPacket>(reader);
}

public class HandsInspectPacketHandler : PacketHandler<HandsInspectPacket>
{
    public void Send() => DispatchPacket(new HandsInspectPacket { Player = H.MainPlayer, });

    protected override void Apply(HandsInspectPacket packet, NetPeer peer)
    {
        if (packet.Player.IsYourPlayer) return;

        if (packet.Player.HandsController is EmptyHandsController emptyHands)
        {
            emptyHands.ExamineWeapon();
        }
    }
}