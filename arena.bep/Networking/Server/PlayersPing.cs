using Comfort.Common;
using Fika.Core.Main.GameMode;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using System.Collections.Generic;
using System.Linq;

namespace ifp.arena.bep.networking
{
    public struct PlayerPingData
    {
        public int playerId;
        public int ping;
    }

    public struct PlayersPingPacket : INetSerializable
    {
        public PlayerPingData[] scores;

        public void Serialize(NetDataWriter writer)
        {
            int length = scores?.Length ?? 0;
            writer.Put(length);
            for (int i = 0; i < length; i++)
            {
                writer.Put(scores[i].playerId);
                writer.Put(scores[i].ping);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            int length = reader.GetInt();
            scores = new PlayerPingData[length];
            for (int i = 0; i < length; i++)
            {
                scores[i] = new PlayerPingData
                {
                    playerId = reader.GetInt(),
                    ping = reader.GetInt(),
                };
            }
        }
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
                    ping = GetNetManager().GetPeerById(kvp.Key).Ping,
                }).ToArray()
            };

            RequestSend(packet);
        }

        public override void WhenApproved(PlayersPingPacket packet, NetPeer peer)
        {
            foreach (var syncScore in packet.scores)
            {
                if (H.Scoreboard.TryGetValue(syncScore.playerId, out var playerScore))
                {
                    playerScore.ping = syncScore.ping;
                }
            }
        }
    }
}