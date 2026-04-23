using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;
using System;
using Comfort.Common;
using ifp.arena.bep.Core;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct ZoneOwnershipPacket : INetSerializable
{
    public int netId;
    public ZoneOwnership ownership;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<ZoneOwnershipPacket>(reader);
}

public class ZoneOwnershipPacketHandler : PacketHandler<ZoneOwnershipPacket>
{
    public ZoneOwnershipPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

    public void Send(int netId, ZoneOwnership ownership)
    {
        var packet = new ZoneOwnershipPacket
        {
            netId = netId,
            ownership = ownership,
        };

        DispatchPacket(packet);
    }

    protected override void WhenApproved(ZoneOwnershipPacket packet, NetPeer peer)
    {
        if (H.IsServer) return;

        Singleton<HardpointZoneManager>.Instance.NetIdtoZone[packet.netId].ChangeOwnership(packet.ownership);
    }
}
