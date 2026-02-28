using Comfort.Common;
using EFT;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Networking.Base;
using System;
using System.Linq;

namespace ifp.arena.bep.Networking
{
    public struct PlayerKilledPacket : INetSerializable
    {
        public int killerId;
        public int victimId;
        public int assistId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(killerId);
            writer.Put(victimId);
            writer.Put(assistId);
        }

        public void Deserialize(NetDataReader reader)
        {
            killerId = reader.GetInt();
            victimId = reader.GetInt();
            assistId = reader.GetInt();
        }

        public override string ToString()
        {
            return $"{killerId} ubil {victimId}";
        }
    }

    public class PlayerKilledPacketHandler : PacketHandler<PlayerKilledPacket>
    {
        public event Action<EFT.Player> OnPlayerKilled;

        public void Send(int killerId, int victimId, int assistId)
        {
            var packet = new PlayerKilledPacket
            {
                killerId = killerId,
                victimId = victimId,
                assistId = assistId
            };

            RequestSend(packet);
        }

        public override void OnReceive(PlayerKilledPacket packet)
        {
            BaseGameMode GameMode = Singleton<BaseGameMode>.Instance;
            if (GameMode?.session == null)
            {
                Plugin.Logger.LogInfo("SessionInfo does not exist type beat");
                return;
            }

            var scoreboard = GameMode.session.scoreboard;

            scoreboard[packet.victimId].deaths++;
            scoreboard[packet.victimId].isAlive = false;

            EFT.Player victimPlayer = Singleton<GameWorld>.Instance.AllAlivePlayersList.FirstOrDefault(p => p.Id == packet.victimId);
            if (victimPlayer != null)
            {
                OnPlayerKilled?.Invoke(victimPlayer);
            }
        }
    }

    public struct AssetLoadStatePacket : INetSerializable
    {
        public int id;
        public bool isReady;
        public string msg;


        public void Serialize(NetDataWriter writer)
        {
            writer.Put(isReady);
            writer.Put(msg);
        }

        public void Deserialize(NetDataReader reader)
        {
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
            var packet = new AssetLoadStatePacket
            {
                id = Singleton<GameWorld>.Instance.MainPlayer.Id,
                isReady = isLoaded,
                msg = msg
            };

            RequestSend(packet);
        }

        public override bool ServerValidation(ref AssetLoadStatePacket packet, NetPeer netPeer)
        {
            packet.id = netPeer.Id;
            return base.ServerValidation(ref packet, netPeer);
        }

        public override void OnReceive(AssetLoadStatePacket packet)
        {
            Singleton<BaseGameMode>.Instance.session.scoreboard[packet.id].isReady = packet.isReady;
        }
    }
}