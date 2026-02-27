using Comfort.Common;
using EFT;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
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
    }

    public class PlayerKilledPacketHandler : PacketHandler<PlayerKilledPacket>
    {
        public static event Action<EFT.Player> OnPlayerKilled;

        public void Send(int killerId, int victimId, int assistId)
        {
            var packet = new PlayerKilledPacket
            {
                killerId = killerId,
                victimId = victimId,
                assistId = assistId
            };

            OnSend(packet);
        }

        public override void OnReceive(PlayerKilledPacket packet)
        {
            BaseGameMode GameMode = Singleton<BaseGameMode>.Instance;
            if (GameMode?.sessionInfo == null) return;

            var scoreboard = GameMode.sessionInfo.scoreboard;

            scoreboard[packet.killerId].kills++;
            scoreboard[packet.victimId].deaths++;

            if (packet.assistId != 1337)
            {
                scoreboard[packet.assistId].assists++;
            }

            EFT.Player victimPlayer = Singleton<GameWorld>.Instance.AllAlivePlayersList.FirstOrDefault(p => p.Id == packet.victimId);
            if (victimPlayer != null)
            {
                OnPlayerKilled?.Invoke(victimPlayer);
            }
        }
    }
}