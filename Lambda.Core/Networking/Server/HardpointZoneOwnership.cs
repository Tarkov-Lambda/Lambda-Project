using PacketWarden;
using MemoryPack;
using Comfort.Common;
using Lambda.Core.Main;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct ZoneOwnershipPacket : IPacket
{
    public int netId;
    public ZoneOwnership ownership;
}

public class ZoneOwnershipPacketWarden : LambdaPacketWarden<ZoneOwnershipPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(int netId, ZoneOwnership ownership)
    {
        var packet = new ZoneOwnershipPacket
        {
            netId = netId,
            ownership = ownership,
        };

        DispatchPacket(packet);
    }

    protected override void Apply(ZoneOwnershipPacket packet, int peerId)
    {
        if (H.IsServer) return;

        Singleton<HardpointZoneManager>.Instance.NetIdtoZone[packet.netId].ChangeOwnership(packet.ownership);
    }
}
