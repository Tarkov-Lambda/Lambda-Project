using Comfort.Common;
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
            var killer = GameMode.sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == packet.killerId);
            if (killer != null) killer.kills++;

            var victim = GameMode.sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == packet.victimId);
            if (victim != null) victim.deaths++;

            if (packet.assistId != 1337)
            {
                var assist = GameMode.sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == packet.assistId);
                if (assist != null) assist.assists++;
            }

            EFT.Player victimPlayer = GameMode.sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == packet.victimId).p;
            OnPlayerKilled?.Invoke(victimPlayer);
        }
    }
}
