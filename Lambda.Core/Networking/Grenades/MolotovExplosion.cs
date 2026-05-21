using Cysharp.Threading.Tasks;
using Lambda.Core.Main;
using PacketWarden;
using MemoryPack;
using UnityEngine;
using System.Collections.Generic;
using System;
using PacketWarden.TimeSync;

namespace Lambda.Core.Networking;

// TODO: REFACTOR STRUCT
// currently this is around 1300 bytes per explosion
[MemoryPackable]
public partial struct MolotovExplosionPacket : IPacket, IServerTimestampedPacket, ITrackablePacket
{
    public Guid ID { get; set; }
    public double Timestamp { get; set; }
    public Vector3 explosionPos;
    public List<FireNode> fireNodes;
}

public class MolotovExplosionPacketWarden : LambdaPacketWarden<MolotovExplosionPacket>
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

        DispatchPacket(ref packet);
    }

    protected override async void Apply(MolotovExplosionPacket packet, int peerId)
    {
        MolotovController.Spawn(packet).Forget();
    }
}