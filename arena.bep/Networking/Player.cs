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
            return $"{killerId} killed {victimId}";
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

        public override void OnReceive(PlayerKilledPacket packet, NetPeer peer)
        {
            //NotificationManagerClass.DisplayMessageNotification($"{packet.ToString()}");

            BaseGameMode GameMode = Singleton<BaseGameMode>.Instance;

            var scoreboard = GameMode.session.scoreboard;

            scoreboard[packet.killerId].kills++;
            scoreboard[packet.victimId].deaths++;

            if (GameMode.session.currentGameMode == GameModes.SND)
            {
                scoreboard[packet.victimId].isAlive = false;
            }

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

        public override void OnReceive(FactionChangePacket packet, NetPeer peer)
        {
            BaseGameMode GameMode = Singleton<BaseGameMode>.Instance;

            var scoreboard = GameMode.session.scoreboard;

            scoreboard[packet.id].faction = packet.faction;
        }
    }
}