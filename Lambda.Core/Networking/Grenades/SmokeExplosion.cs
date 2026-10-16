using PacketWarden;
using MemoryPack;
using UnityEngine;
using PacketWarden.TimeSync;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct SmokeExplosionPacket : IPacket, IServerTimestampedPacket
{
    public double Timestamp { get; set; }
    public Vector3 explosionPos;
}

public class SmokeExplosionPacketWarden : LambdaPacketWarden<SmokeExplosionPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(Vector3 explosionPos)
    {
        var packet = new SmokeExplosionPacket
        {
            Timestamp = NetworkTime.ServerNowSeconds,
            explosionPos = explosionPos,
        };

        DispatchPacket(ref packet);
    }

    protected override void Apply(SmokeExplosionPacket packet, int peerId) { }
}