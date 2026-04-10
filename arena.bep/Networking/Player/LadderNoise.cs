using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using ifp.arena.shared;
using MemoryPack;
using UnityEngine;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct LadderNoisePacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public LadderMaterial ladderMaterial;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<LadderNoisePacket>(reader);
}

public class LadderNoisePacketHandler : PacketHandler<LadderNoisePacket>
{
    public void Send(LadderMaterial ladderMaterial) => RequestSend(new LadderNoisePacket { ladderMaterial = ladderMaterial });

    protected override void LocalPredictApproved(LadderNoisePacket packet)
    {
        MakeLadderNoise(H.MainPlayer, packet);
    }

    protected override void WhenApproved(LadderNoisePacket packet, NetPeer peer)
    {
        if (packet.Player.IsYourPlayer) return;

        MakeLadderNoise(packet.Player, packet);
    }

    private void MakeLadderNoise(Player player, LadderNoisePacket packet)
    {
        Vector3 pos = player.PlayerBody.transform.position;
        AudioClip[] audioClips = packet.ladderMaterial == LadderMaterial.Metal ? H.Sounds.LadderNoiseMetal : H.Sounds.LadderNoiseWood;

        H.AudioHandler.PlayAtPoint(pos, audioClips.RandomElement());
    }
}

