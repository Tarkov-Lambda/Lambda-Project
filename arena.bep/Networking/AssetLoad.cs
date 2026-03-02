using Comfort.Common;
using EFT;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using System;
using System.Linq;

namespace ifp.arena.bep.networking
{
    public struct AssetLoadStatePacket : INetSerializable
    {
        public int id;
        public bool isReady;
        public string msg;


        public void Serialize(NetDataWriter writer)
        {
            writer.Put(id);
            writer.Put(isReady);
            writer.Put(msg);
        }

        public void Deserialize(NetDataReader reader)
        {
            id = reader.GetInt();
            isReady = reader.GetBool();
            msg = reader.GetString();

        }

        public override string ToString()
        {
            return $"{isReady}";
        }
    }

    public class AssetLoadStatePacketHandler : PacketHandler<AssetLoadStatePacket>
    {
        public void Send(bool isLoaded, string msg)
        {
            var mainPlayer = Singleton<GameWorld>.Instance?.MainPlayer;
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

        public override bool ServerValidation(ref AssetLoadStatePacket packet, NetPeer netPeer)
        {
            return base.ServerValidation(ref packet, netPeer);
        }

        public override void OnReceive(AssetLoadStatePacket packet, NetPeer peer)
        {
            if (Singleton<BaseGameMode>.Instance.session.scoreboard.TryGetValue(packet.id, out var playerScore))
            {
                playerScore.isReady = packet.isReady;
            }
            else
            {
                Plugin.Logger.LogError($"Player {packet.id} not found in scoreboard!");
            }
        }
    }
}