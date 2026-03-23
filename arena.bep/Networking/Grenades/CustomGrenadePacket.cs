using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT.Interactive;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.shared.FX;
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

        protected override async void WhenApproved(CustomGrenadeExplosionPacket packet, NetPeer peer)
        {
            // Singleton<RaymarchHandler>.Instance.Raymarcher.smokeVoxelData.HandleSmokeThrow(packet.explosionPos);
            GameObject molotov = new GameObject("Molotov");
            molotov.transform.position = packet.explosionPos;


            SphereCollider sCollider = molotov.AddComponent<SphereCollider>();

            float duration = 2f;
            float elapsed = 0f;

            float startRadius = 1f;
            float endRadius = 3f;

            FlameDamageTrigger flameDamageTrigger = molotov.AddComponent<FlameDamageTrigger>();
            MolotovFXController molotovFX = Singleton<FXHandler>.Instance.SpawnMolotov(packet.explosionPos, startRadius, endRadius, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = elapsed / duration;
                sCollider.radius = Mathf.Lerp(startRadius, endRadius, t);

                await UniTask.Yield();
            }

            sCollider.radius = endRadius;

            await UniTask.WaitForSeconds(7);

            molotovFX.StopAndFadeOut();
            GameObject.DestroyImmediate(flameDamageTrigger);
            GameObject.DestroyImmediate(molotov);
        }
    }
}
