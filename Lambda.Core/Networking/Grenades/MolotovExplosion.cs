using Lambda.Core.Main;
using PacketWarden;
using MemoryPack;
using UnityEngine;
using PacketWarden.TimeSync;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct MolotovExplosionPacket : IPacket, IServerTimestampedPacket
{
    public double Timestamp { get; set; }
    public Vector3 explosionPos;
    public float fireRadius;
    public FireNode[] fireNodes;
}

public class MolotovExplosionPacketWarden : LambdaPacketWarden<MolotovExplosionPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(Vector3 explosionPos)
    {
        var packet = new MolotovExplosionPacket
        {
            Timestamp = NetworkTime.ServerNowSeconds,
            explosionPos = explosionPos,
            fireRadius = GameplayVariables.vars.FireRadius,
            fireNodes = MolotovController.GenerateFireSpread(explosionPos)
        };

        DispatchPacket(ref packet);
    }

    protected override void Apply(MolotovExplosionPacket packet, int peerId)
    {
        MolotovController.Spawn(packet);
    }
}