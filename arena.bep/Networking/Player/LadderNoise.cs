using Comfort.Common;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using MemoryPack;
using UnityEngine;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct LadderNoisePacket : INetSerializable
{
    [MemoryPackAllowSerialize]
    public Player player { get; set; }

    public LadderMaterial ladderMaterial;

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<LadderNoisePacket>(reader);
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
        if (packet.player.IsYourPlayer) return;

        MakeLadderNoise(packet.player, packet);
    }

    private void MakeLadderNoise(Player player, LadderNoisePacket packet)
    {
        Vector3 pos = player.PlayerBody.transform.position;
        AudioClip[] audioClips = packet.ladderMaterial == LadderMaterial.Metal ? H.Sounds.LadderNoiseMetal : H.Sounds.LadderNoiseWood;

        H.AudioHandler.PlayAtPoint(pos, audioClips.RandomElement());
    }
}

