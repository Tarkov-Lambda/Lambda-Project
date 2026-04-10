using Cysharp.Threading.Tasks;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using PacketHandler;
using ifp.arena.bep.networking.TimeSync;
using MemoryPack;
using UnityEngine;

namespace ifp.arena.bep.networking;

public enum CustomGrenadeType
{
    Molotov,
    Smoke
}

[MemoryPackable]
public partial struct CustomGrenadeExplosionPacket : INetSerializable
{
    public double serverSendSeconds;
    public Vector3 explosionPos;
    public CustomGrenadeType grenadeType;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<CustomGrenadeExplosionPacket>(reader);
}

public class CustomGrenadeExplosionPacketHandler : PacketHandler<CustomGrenadeExplosionPacket>
{
    public CustomGrenadeExplosionPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

    public void Send(Vector3 explosionPos, CustomGrenadeType grenadeType)
    {
        var packet = new CustomGrenadeExplosionPacket
        {
            serverSendSeconds = NetworkTime.LocalNowSeconds,
            explosionPos = explosionPos,
            grenadeType = grenadeType
        };

        RequestSend(packet);
    }

    protected override async void WhenApproved(CustomGrenadeExplosionPacket packet, NetPeer peer)
    {
        // Singleton<RaymarchHandler>.Instance.Raymarcher.smokeVoxelData.HandleSmokeThrow(packet.explosionPos);
        Molotov.Spawn(packet).Forget();
    }
}