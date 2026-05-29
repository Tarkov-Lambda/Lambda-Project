using EFT;
using Lambda.Core.Main.Gamemode;
using Lambda.Shared;
using MemoryPack;
using UnityEngine;
using PacketWarden.TimeSync;
using Comfort.Common;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct BombStatePacket : IPacket, IAuthoredPacket, IServerTimestampedPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public double Timestamp { get; set; }

    public BombState state;

    public Vector3 position;
}

public class BombStatePacketWarden : LambdaPacketWarden<BombStatePacket>
{
    public void Send(Player player, BombState state, Vector3 position)
    {
        var packet = new BombStatePacket
        {
            Player = player,
            state = state,
            position = position,
            Timestamp = NetworkTime.ServerNowSeconds
        };

        DispatchPacket(ref packet);
    }

    protected override void MutateApprovedPacket(ref BombStatePacket packet, int peerId)
    {
        packet.Timestamp = NetworkTime.ServerNowSeconds;
    }

    protected override void ApplyOptimistically(BombStatePacket packet)
    {
        // idk how I feel about mutating the state like this locally as a general practice
        // but it shouldn't be an issue here at least
        if (packet.state == BombState.Planted)
        {
            H.BombHandler.BombPlantedPosition = packet.position;
            H.BombHandler.BombVisuals.transform.position = packet.position;
        }

        H.BombHandler.PlayBombAudio(packet);
    }

    protected override void Apply(BombStatePacket packet, int peerId)
    {
        H.Session.bombState = packet.state;

        if (!H.IsHeadless)
        {
            if (!packet.Player.IsYourPlayer)
            {
                H.BombHandler.PlayBombAudio(packet);
            }
        }

        if (packet.state is BombState.Planted)
        {
            H.Arena.LastObjectivePlayer = packet.Player;
            foreach (var bombPlantZone in Object.FindObjectsByType<BombPlantZone>(FindObjectsSortMode.None))
            {
                bombPlantZone.GetComponent<BoxCollider>().enabled = false; // remove interaction triggers
            }
        }


        if (packet.state is BombState.Defused or BombState.Exploded)
        {
            H.Arena.LastObjectiveBombState = packet.state;
            if (packet.Player != null) H.Arena.LastObjectivePlayer = packet.Player;
        }

        if (H.IsServer && packet.state is BombState.Exploded)
        {
            var damageInfo = new DamageInfoStruct
            {
                Damage = 900f,
                BodyPartColliderType = EBodyPartColliderType.RibcageUp,
                DamageType = EDamageType.Explosion,
            };
            foreach (var player in H.AllPlayers)
            {
                float distance = Vector3.Distance(packet.position, player.PlayerBody.transform.position);
                if (distance <= 30f)
                {
                    Singleton<PlayerKilledPacketWarden>.Instance.Send(damageInfo, player, H.Arena.LastObjectivePlayer);
                }
            }
        }

        H.BombHandler.SetBombVisuals(packet);
        EventBus.OnBombStateChange(packet.state);
    }
}