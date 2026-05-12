using Cysharp.Threading.Tasks;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using PacketHandler;
using MemoryPack;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct SmokeExplosionPacket : INetSerializable, IServerTimestampedPacket, ITrackable
{
    public Guid ID { get; set; }
    public double Timestamp { get; set; }
    public Vector3 explosionPos;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<SmokeExplosionPacket>(reader);
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
            Timestamp = Time.unscaledTime,
            explosionPos = explosionPos,
        };

        DispatchPacket(packet);
    }

    protected override async void Apply(SmokeExplosionPacket packet, NetPeer peer)
    {
        
    }
}