using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT.Interactive;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.TimeSync;
using MemoryPack;
using System;
using UnityEngine;

namespace ifp.arena.bep.networking
{
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

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<CustomGrenadeExplosionPacket>(reader);
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

        public override async void WhenApproved(CustomGrenadeExplosionPacket packet, NetPeer peer)
        {
            GameObject molotov = new GameObject("Molotov");
            molotov.transform.position = packet.explosionPos;

            SphereCollider sCollider = molotov.AddComponent<SphereCollider>();
            sCollider.radius = 5f;

            FlameDamageTrigger flameDamageTrigger = molotov.AddComponent<FlameDamageTrigger>();

            Action disableFireEffect = Singleton<FXHandler>.Instance.SpawnMolotov(packet.explosionPos);

            await UniTask.WaitForSeconds(3000);

            disableFireEffect?.Invoke();
            GameObject.Destroy(molotov);
        }
    }
}
