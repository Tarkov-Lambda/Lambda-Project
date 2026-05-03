using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using ifp.arena.shared;
using MemoryPack;
using UnityEngine;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct DictateTeleport : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public Vector3 position;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<DictateTeleport>(reader);
}

public class DictateTeleportHandler : PacketHandler<DictateTeleport>
{
    public DictateTeleportHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

    public void SendToPlayer(Player player, Vector3 position)
    {
        var packet = new DictateTeleport
        {
            Player = player,
            position = position
        };

        DispatchPacketToPlayer(packet, player);
    }

    protected override void ProcessApprovedPacket(ref DictateTeleport packet, NetPeer peer)
    {
        MutateApprovedPacket(ref packet, peer);
        if (!packet.Player.IsAI)
        {
            H.FikaNet.SendData(ref packet, deliveryMethod, true);
        }
        ApplyInternal(packet, peer);
    }

    protected override void Apply(DictateTeleport packet, NetPeer peer)
    {
        // for AI teleportation
        if (H.IsServer)
        {
            if (packet.Player.IsAI)
            {
                packet.Player.Teleport(packet.position);
            }
        }

        if (!H.IsHeadless)
        {
            if (!packet.Player.IsYourPlayer) return;
            packet.Player.Teleport(packet.position);
        }
    }
}