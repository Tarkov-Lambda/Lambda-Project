using Comfort.Common;
using EFT;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using MemoryPack;
using System;
using System.Linq;

namespace ifp.arena.bep.networking
{
    [MemoryPackable]
    public partial struct AssetLoadStatePacket : INetSerializable
    {
        public int id;
        public bool isReady;
        public string msg;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);

        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<AssetLoadStatePacket>(reader);
    }

    public class AssetLoadStatePacketHandler : PacketHandler<AssetLoadStatePacket>
    {
        public void Send(bool isLoaded, string msg)
        {
            var mainPlayer = H.GameWorld?.MainPlayer;
            if (mainPlayer == null)
            {
                return;
            }

            var packet = new AssetLoadStatePacket
            {
                id = mainPlayer.Id,
                isReady = isLoaded,
                msg = msg
            };

            RequestSend(packet);
        }

        public override void WhenApproved(AssetLoadStatePacket packet, NetPeer peer)
        {
            var playerScore = H.GetPlayerScore(packet.id);
            if (playerScore != null)
            {
                playerScore.isMapReady = packet.isReady;
            }
            else
            {
                Plugin.Logger.LogError($"Player {packet.id} not found in scoreboard!");
            }
        }
    }
}
