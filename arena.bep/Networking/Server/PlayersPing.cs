using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;
using System.Linq;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PlayerPingData
{
    public int playerId;
    public int ping;
}

[MemoryPackable]
public partial struct PlayersPingPacket : INetSerializable
{
    public PlayerPingData[] scores;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<PlayersPingPacket>(reader);
}

// This runs on interval
public class PlayersPingPacketHandler : PacketHandler<PlayersPingPacket>
{
    public PlayersPingPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

    public void Send()
    {
        var packet = new PlayersPingPacket
        {
            scores = H.Scoreboard.Select(kvp => new PlayerPingData
            {
                playerId = kvp.Key,
                ping = H.NetManager.GetPeerById(kvp.Key).Ping,
            }).ToArray()
        };

        DispatchPacket(packet);
    }

    protected override void WhenApproved(PlayersPingPacket packet, NetPeer peer)
    {
        foreach (var syncScore in packet.scores)
        {
            if (H.Scoreboard.TryGetValue(syncScore.playerId, out var playerScore))
            {
                // playerScore.ping = syncScore.ping;
            }
        }
    }
}