using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using ifp.arena.shared;
using MemoryPack;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct DictateTeleportPacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public Vector3 position;
    public Quaternion rotation;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<DictateTeleportPacket>(reader);
}

public class DictateTeleportPacketHandler : LambdaPacketHandler<DictateTeleportPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void SendToPlayer(Player player, Vector3 position, Quaternion rotation)
    {
        var packet = new DictateTeleportPacket
        {
            Player = player,
            position = position,
            rotation = rotation
        };

        DispatchPacketToPlayer(packet, player);
    }

    protected override void ProcessApprovedPacket(ref DictateTeleportPacket packet, NetPeer peer)
    {
        MutateApprovedPacket(ref packet, peer);
        if (!packet.Player.IsAI)
        {
            H.FikaNet.SendData(ref packet, DeliveryMethod, true);
        }
        ApplyInternal(packet, peer);
    }

    // duct tape for now
    public void Apply(DictateTeleportPacket packet) => Apply(packet, null);

    protected override void Apply(DictateTeleportPacket packet, NetPeer peer)
    {
        if (packet.Player.IsYourPlayer || (H.IsServer && packet.Player.IsAI))
        {
            packet.Player.Teleport(packet.position);

            Vector3 euler = packet.rotation.eulerAngles;
            Vector2 lookRotation = new Vector2(euler.y, Mathf.DeltaAngle(0f, euler.x));

            packet.Player.Transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
            packet.Player.Rotation = lookRotation;
            packet.Player.MovementContext.CachedRotation = lookRotation;

            UniTask.RunOnThreadPool(async () =>
            {
                await UniTask.Delay(1500);
                if (!packet.Player.MovementContext.IsGrounded)
                {
                    var canWeGetMuchHigher = packet.position;
                    canWeGetMuchHigher.y += 1.25f;
                    packet.Player.MovementContext.ResetFlying();
                    packet.Player.Teleport(canWeGetMuchHigher);
                    await UniTask.DelayFrame(1);
                    packet.Player.MovementContext.ResetFlying();
                }
            });
        }
    }
}