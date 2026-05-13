using Cysharp.Threading.Tasks;
using Lambda.Core.Main;
using PacketHandler;
using MemoryPack;
using UnityEngine;
using System.Collections.Generic;
using System;
using PacketHandler.TimeSync;

namespace Lambda.Core.Networking;

// TODO: REFACTOR STRUCT
// currently this is around 1300 bytes per explosion
[MemoryPackable]
public partial struct MolotovExplosionPacket : IPacket, IServerTimestampedPacket, ITrackable
{
    public Guid ID { get; set; }
    public double Timestamp { get; set; }
    public Vector3 explosionPos;
    public List<FireNode> fireNodes;
}

public class MolotovExplosionPacketHandler : LambdaPacketHandler<MolotovExplosionPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(Vector3 explosionPos)
    {
        List<FireNode> generatedNodes = MolotovController.GenerateFireSpread(explosionPos);

        var packet = new MolotovExplosionPacket
        {
            ID = Guid.NewGuid(),
            Timestamp = NetworkTime.ServerNowSeconds,
            explosionPos = explosionPos,
            fireNodes = generatedNodes
        };

        DispatchPacket(packet);
    }

    protected override async void Apply(MolotovExplosionPacket packet, int peerId)
    {
        MolotovController.Spawn(packet).Forget();
    }
}