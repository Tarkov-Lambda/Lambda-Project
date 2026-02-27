using Comfort.Common;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using System;
using System.Linq;

namespace ifp.arena.bep.Networking
{
    public struct PlayerKilledPacket : INetSerializable
    {
        public int KillerId;
        public int VictimId;
        public int AssistId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(KillerId);
            writer.Put(VictimId);
            writer.Put(AssistId);
        }

        public void Deserialize(NetDataReader reader)
        {
            KillerId = reader.GetInt();
            VictimId = reader.GetInt();
            AssistId = reader.GetInt();
        }
    }

    public class PlayerKilledPacketHandler : PacketHandler<PlayerKilledPacket>
    {
        public static event Action<EFT.Player> OnPlayerKilled;

        public override void OnReceive(PlayerKilledPacket packet)
        {
            BaseGameMode GameMode = Singleton<BaseGameMode>.Instance;
            var killer = GameMode.sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == packet.KillerId);
            if (killer != null) killer.kills++;

            var victim = GameMode.sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == packet.VictimId);
            if (victim != null) victim.deaths++;

            if (packet.AssistId != 1337)
            {
                var assist = GameMode.sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == packet.AssistId);
                if (assist != null) assist.assists++;
            }

            EFT.Player victimPlayer = GameMode.sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == packet.VictimId).p;
            OnPlayerKilled?.Invoke(victimPlayer);
        }
    }
}
