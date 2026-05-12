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

// TODO: REFACTOR STRUCT
// currently this is around 1300 bytes per explosion
[MemoryPackable]
public partial struct MolotovExplosionPacket : INetSerializable, IServerTimestampedPacket, ITrackable
{
    public Guid ID { get; set; }
    public double Timestamp { get; set; }
    public Vector3 explosionPos;
    public List<FireNode> fireNodes;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<MolotovExplosionPacket>(reader);
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

    protected override async void Apply(MolotovExplosionPacket packet, NetPeer peer)
    {
        MolotovController.Spawn(packet).Forget();
    }
}