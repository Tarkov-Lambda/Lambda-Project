using EFT;
using ifp.arena.shared;
using MemoryPack;
using UnityEngine;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct LadderNoisePacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public LadderMaterial ladderMaterial;
}

public class LadderNoisePacketHandler : LambdaPacketHandler<LadderNoisePacket>
{
    public void Send(LadderMaterial ladderMaterial)
    {
        var packet = new LadderNoisePacket
        {
            Player = H.MainPlayer,
            ladderMaterial = ladderMaterial
        };

        DispatchPacket(packet);
    }

    protected override void LocalPredictApproved(LadderNoisePacket packet)
    {
        MakeLadderNoise(H.MainPlayer, packet);
    }

    protected override void Apply(LadderNoisePacket packet, int peerId)
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

