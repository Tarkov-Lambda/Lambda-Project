using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;

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

    public class BaseGameMode : IDisposable
    {
        public float RoundTime;
        public SessionInfo sessionInfo;

        public Action<EFT.Player> OnPlayerKilled;


        // Shit starts happening
        public void startSession(GameWorld gameWorld)
        {
            Plugin.Logger.LogInfo("startSession");

            if (Singleton<GameWorld>.Instance == null)
            {
                Singleton<IFikaNetworkManager>.Instance.RegisterPacket<PlayerKilledPacket>(OnPlayerKilledPacketReceived);
            }

            sessionInfo = new SessionInfo();
            foreach (var p in gameWorld.AllAlivePlayersList.ToArray())
            {
                Plugin.Logger.LogInfo(p.Id);
                PlayerScore newPlayer = new PlayerScore(p);
                sessionInfo.scoreboard.Add(newPlayer);
            }
        }

        // Shit stops happening
        public void endSession()
        {

        }

        public BaseGameMode()
        {
            if (Singleton<GameWorld>.Instance != null)
            {
                startSession(Singleton<GameWorld>.Instance);
            }
            Patch_Gameworld_OnGameStarted.OnGameStarted += startSession;

            //FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(OnNetworkManagerCreated);

        }

        public void Dispose()
        {
            if (Singleton<IFikaNetworkManager>.Instance != null)
            {
                //Singleton<IFikaNetworkManager>.Instance.UnregisterPacket<PlayerKilledPacket>();
            }
            Patch_Gameworld_OnGameStarted.OnGameStarted -= startSession;
        }

        private void OnNetworkManagerCreated(FikaNetworkManagerCreatedEvent evt = null)
        {
            Singleton<IFikaNetworkManager>.Instance.RegisterPacket<PlayerKilledPacket>(OnPlayerKilledPacketReceived);
        }

        public void reportLocalDeath(int killerId, int victimId, int assistId = 1337)
        {
            Plugin.Logger.LogInfo("reportLocalDeath");

            var packet = new PlayerKilledPacket
            {
                KillerId = killerId,
                VictimId = victimId,
                AssistId = assistId
            };
            Plugin.Logger.LogInfo("packet made");
            Plugin.Logger.LogInfo(packet.VictimId);

            Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, DeliveryMethod.ReliableOrdered, FikaBackendUtils.IsServer);

            if (FikaBackendUtils.IsServer)
            {
                OnPlayerKilledPacketReceived(packet);
            }
        }

        private void OnPlayerKilledPacketReceived(PlayerKilledPacket packet)
        {
            Plugin.Logger.LogInfo("OnPlayerKilledPacketReceived");
            Plugin.Logger.LogInfo(packet.VictimId);
            registerKill(packet.KillerId, packet.VictimId, packet.AssistId);

            if (FikaBackendUtils.IsServer)
            {
                var manager = Singleton<IFikaNetworkManager>.Instance;
                manager.SendData(ref packet, DeliveryMethod.ReliableOrdered, true);
            }

            EFT.Player victimPlayer = sessionInfo.scoreboard.FirstOrDefault(p => p.p.Id == packet.VictimId).p;
            Plugin.Logger.LogInfo($"Victim Name: {victimPlayer.name}");

            OnPlayerKilled?.Invoke(victimPlayer);
        }

        public void registerKill(int killerId, int victimId, int assistId = 1337)
        {
            Plugin.Logger.LogInfo("registerKill");
            Plugin.Logger.LogInfo(victimId);
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
