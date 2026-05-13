using Lambda.Core.Main;
using PacketHandler;
using MemoryPack;
using UnityEngine;
using System.Collections.Generic;
using System;
using PacketHandler.TimeSync;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct SmokeExplosionPacket : IPacket, IServerTimestampedPacket, ITrackable
{
    public Guid ID { get; set; }
    public double Timestamp { get; set; }
    public Vector3 explosionPos;
}

public class SmokeExplosionPacketHandler : LambdaPacketHandler<SmokeExplosionPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(Vector3 explosionPos)
    {
        List<FireNode> generatedNodes = MolotovController.GenerateFireSpread(explosionPos);

        var packet = new SmokeExplosionPacket
        {
            ID = Guid.NewGuid(),
            Timestamp = NetworkTime.ServerNowSeconds,
            explosionPos = explosionPos,
        };

        DispatchPacket(packet);
    }

    protected override async void Apply(SmokeExplosionPacket packet, int peerId)
    {
        
    }
}