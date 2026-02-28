using Comfort.Common;
using EFT;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Networking.Base;
using ifp.arena.shared;
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

    public struct FactionChangePacket : INetSerializable
    {
        public int id;
        public Faction faction;


        public void Serialize(NetDataWriter writer)
        {
            writer.Put(id);
            writer.Put((int)faction);

        }

        public void Deserialize(NetDataReader reader)
        {
            id = reader.GetInt();
            faction = (Faction)reader.GetInt();
        }

        public override string ToString()
        {
            return $"{id} changed faction to {faction}";
        }
    }

    public class FactionChangePacketHandler : PacketHandler<FactionChangePacket>
    {
        public void Send(Faction faction)
        {
            var packet = new FactionChangePacket
            {
                id = Singleton<GameWorld>.Instance.MainPlayer.Id,
                faction = faction
            };

            RequestSend(packet);
        }

        public override void OnReceive(FactionChangePacket packet)
        {
            BaseGameMode GameMode = Singleton<BaseGameMode>.Instance;

            var scoreboard = GameMode.session.scoreboard;

            scoreboard[packet.id].faction = packet.faction;
        }
    }

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
            Plugin.Logger.LogInfo($"Sending AssetLoadStatus");
            var packet = new AssetLoadStatePacket
            {
                id = Singleton<GameWorld>.Instance.MainPlayer.Id + 1,
                isReady = isLoaded,
                msg = msg
            };

            RequestSend(packet);
        }

        public override bool ServerValidation(ref AssetLoadStatePacket packet, NetPeer netPeer)
        {
            //packet.id = netPeer.Id;
            return base.ServerValidation(ref packet, netPeer);
        }

        public override void OnReceive(AssetLoadStatePacket packet)
        {
            if (Singleton<BaseGameMode>.Instance.session.scoreboard.TryGetValue(packet.id, out var playerScore))
            {
                Plugin.Logger.LogInfo(playerScore.faction.ToString());
                Plugin.Logger.LogInfo(playerScore.isReady);

                playerScore.isReady = packet.isReady;
            }
            else
            {
                Plugin.Logger.LogError($"Player {packet.id} not found in scoreboard!");
            }
        }
    }
}