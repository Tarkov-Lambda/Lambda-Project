using Comfort.Common;
using Fika.Core.Main.Utils;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifp.arena.bep.GameTypes
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

    public class BaseGameMode
    {
        public float RoundTime;
        public SessionInfo sessionInfo;

        public Action<int> OnPlayerKilled;


        // Shit starts happening
        public void startSession()
        {
        }

        // Shit stops happening
        public void endSession()
        {

        }

        public BaseGameMode()
        {
            FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(OnNetworkManagerCreated);
        }

        private void OnNetworkManagerCreated(FikaNetworkManagerCreatedEvent evt)
        {
            sessionInfo = new SessionInfo();
            EFT.Player[] players = UnityEngine.GameObject.FindObjectsByType<EFT.Player>(UnityEngine.FindObjectsSortMode.None);

            foreach (var p in players)
            {
                PlayerScore newPlayer = new PlayerScore(p);
                Plugin.Logger.LogInfo(p.Id);
                sessionInfo.scoreboard.Add(newPlayer);
            }

            evt.Manager.RegisterPacket<PlayerKilledPacket>(OnPlayerKilledPacketReceived);
        }

        public void reportLocalDeath(int killerId, int victimId, int assistId = 1337)
        {
            var packet = new PlayerKilledPacket
            {
                KillerId = killerId,
                VictimId = victimId,
                AssistId = assistId
            };

            Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, DeliveryMethod.ReliableOrdered, FikaBackendUtils.IsServer);

            if (FikaBackendUtils.IsServer)
            {
                OnPlayerKilledPacketReceived(packet);
            }
        }

        private void OnPlayerKilledPacketReceived(PlayerKilledPacket packet)
        {
            registerKill(packet.KillerId, packet.VictimId, packet.AssistId);
            Plugin.Logger.LogInfo(packet.KillerId);
            Plugin.Logger.LogInfo(packet.VictimId);

            if (FikaBackendUtils.IsServer)
            {
                var manager = Singleton<IFikaNetworkManager>.Instance;
                manager.SendData(ref packet, DeliveryMethod.ReliableOrdered, true);
            }

            OnPlayerKilled?.Invoke(packet.VictimId);
        }

        public void registerKill(int killerId, int victimId, int assistId = 1337)
        {
            var killer = sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == killerId);
            if (killer != null) killer.kills++;

            var victim = sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == victimId);
            if (victim != null) victim.deaths++;

            if (assistId != 1337)
            {
                var assist = sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == assistId);
                if (assist != null) assist.assists++;
            }
        }

        // Server decision
        public virtual void roundEnd(Faction faction = Faction.None)
        {

        }

        public virtual void shouldSessionEnd()
        {

        }
    }

    public class SessionInfo(GameModes gameMode = GameModes.SND)
    {
        public List<PlayerScore> scoreboard = new List<PlayerScore>();
        public GameModes currentGameMode = gameMode;
    }

    public class PlayerScore
    {
        public PlayerScore(EFT.Player player)
        {
            p = player;
        }

        public EFT.Player p;
        public Faction faction = Faction.None;
        public int kills = 0;
        public int assists = 0;
        public int deaths = 0;
    }
}
