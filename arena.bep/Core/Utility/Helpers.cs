using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Patches.Tarkov;
using System.Runtime.CompilerServices;

namespace ifp.arena.bep.Core
{
    // Helper class for singleton refences & helper functions
    public static class H
    {
        public static GameWorld GameWorld => Singleton<GameWorld>.Instance;
        public static Player MainPlayer => isInRaid() ? GameWorld.MainPlayer : null;
        public static Inventory MainInventory => isInRaid() ? MainPlayer.Inventory : null;
        public static InventoryController MainInventoryController => isInRaid() ? MainPlayer.InventoryController : null;

        public static PlayerScore MainPlayerScore => GetMainPlayerScore();
        public static List<Player> AllPlayers => isInRaid() ? GetAllPlayers() : new();

        public static IFikaNetworkManager FikaNet => Singleton<IFikaNetworkManager>.Instance;
        public static NetPacketProcessor NetPacketProcessor => GetPacketProcessor();
        public static NetManager NetManager => GetNetManager();

        public static ArenaController Arena => Singleton<ArenaController>.Instance;
        public static SessionInfo Session => Singleton<ArenaController>.Instance.session;
        public static Dictionary<int, PlayerScore> Scoreboard => Singleton<ArenaController>.Instance.session.scoreboard;

        public static event Action<GameWorld> OnGameStarted
        {
            add => Patch_Gameworld_OnGameStarted.OnGameStarted += value;
            remove => Patch_Gameworld_OnGameStarted.OnGameStarted -= value;
        }

        public static event Action<GameWorld> OnGameDispose
        {
            add => Patch_Gameworld_OnDispose.OnDispose += value;
            remove => Patch_Gameworld_OnDispose.OnDispose -= value;
        }

        public static void Notify(object msg) => NotificationManagerClass.DisplayMessageNotification(msg.ToString());
        public static void NotifyLong(string msg) => NotificationManagerClass.DisplayMessageNotification(msg, EFT.Communications.ENotificationDurationType.Long);

        public static void Log(string msg) => Plugin.Logger.LogInfo(msg);

        // public static void PlayMusic(MusicEvent musicEvent) => MusicManager.Instance?.PlayEvent(musicEvent);
        // public static void PlayMusic(MusicEvent musicEvent) => H.Notify(musicEvent.ToString());

        // bro thinks he's the main character
        public static Player GetMainPlayer()
        {
            if (!isInRaid()) return null;
            return GameWorld.MainPlayer;
        }

        public static Player GetPlayer(int playerId)
        {
            if (!isInRaid()) return null;
            return H.AllPlayers.FirstOrDefault(p => p.Id == playerId);
        }

        public static PlayerScore GetPlayerScore(int playerId)
        {
            if (!isInRaid()) return null;
            Arena.session.scoreboard.TryGetValue(playerId, out var playerScore);
            return playerScore;
        }

        public static PlayerScore GetMainPlayerScore()
        {
            if (!isInRaid()) return null;
            Arena.session.scoreboard.TryGetValue(H.MainPlayer.Id, out var playerScore);
            return playerScore;
        }

        private static List<Player> GetAllPlayers()
        {
            return GameWorld.AllAlivePlayersList;
        }

        public static bool isInRaid()
        {
            return GameWorld != null && GameWorld is not HideoutGameWorld;
        }


        public static NetPacketProcessor GetPacketProcessor()
        {
            var manager = FikaNet;
            if (manager == null) return null;

            var field = AccessTools.Field(FikaNet.GetType(), "_packetProcessor");

            return field?.GetValue(manager) as NetPacketProcessor;
        }

        public static NetManager GetNetManager()
        {
            var manager = H.FikaNet;
            if (manager == null) return null;

            var field = AccessTools.Field(H.FikaNet.GetType(), "_netServer");

            return field?.GetValue(manager) as NetManager;
        }


        public static void Dump(object obj, string msg = "", [CallerArgumentExpression("obj")] string name = null)
        {
            if (obj == null) return;

            var type = obj.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var sb = new StringBuilder();
            sb.Append(msg).Append("\n");
            sb.Append(name).Append(" : ").Append(type.Name).Append("\n{ ");

            foreach (var f in fields)
            {
                var value = f.GetValue(obj);
                sb.Append("\n   ");
                sb.Append(f.Name).Append("=").Append(value).Append(",");
            }

            sb.Append("\n}");

            H.Log(sb.ToString());
        }
    }
}