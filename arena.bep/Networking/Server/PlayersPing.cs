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
public partial struct PlayersPingPacket : IPacket
{
    public PlayerPingData[] scores;
}

// This runs on interval
public class PlayersPingPacketHandler : LambdaPacketHandler<PlayersPingPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

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

    protected override void Apply(PlayersPingPacket packet, int peerId)
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