using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using PacketHandler;
using ifp.arena.shared;
using ifp.arena.bep.networking.TimeSync;
using MemoryPack;
using UnityEngine;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct BombStatePacket : INetSerializable, IAuthoredPacket, IServerTimestampedPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public double Timestamp { get; set; }

    public BombState state;

    public Vector3 position;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<BombStatePacket>(reader);
}

public class BombStatePacketHandler : PacketHandler<BombStatePacket>
{
    public void Send(Player player, BombState state, Vector3 position)
    {
        var packet = new BombStatePacket
        {
            Player    = player,
            state     = state,
            position  = position,
            Timestamp = NetworkTime.ServerNowSeconds
        };

        DispatchPacket(packet);
    }

    protected override void MutateApprovedPacket(ref BombStatePacket packet, NetPeer peer)
    {
        packet.Timestamp = NetworkTime.ServerNowSeconds;
    }

    protected override void LocalPredictApproved(BombStatePacket packet)
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

    protected override void Apply(BombStatePacket packet, NetPeer peer)
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
            H.Arena.LastObjectivePlayerId = packet.Player.Id;
            foreach (var bombPlantZone in Object.FindObjectsByType<BombPlantZone>(FindObjectsSortMode.None))
            {
                bombPlantZone.GetComponent<BoxCollider>().enabled = false;
            }
        }


        if (packet.state is BombState.Defused or BombState.Exploded)
        {
            H.Arena.LastObjectiveBombState = packet.state;
            if (packet.Player.Id > 0) H.Arena.LastObjectivePlayerId = packet.Player.Id;
        }

        H.BombHandler.SetBombVisuals(packet);
        EventBus.OnBombStateChange(packet.state);
    }
}