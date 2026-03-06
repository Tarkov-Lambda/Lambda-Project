

using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using ifp.arena.bep.Core.Audio;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Patches.Tarkov;

namespace ifp.arena.bep.Core
{
    // Helper class for singleton refences & helper functions
    public static class H
    {
        public static GameWorld GameWorld => Singleton<GameWorld>.Instance;
        public static IFikaNetworkManager FikaNet => Singleton<IFikaNetworkManager>.Instance;
        public static Player MainPlayer => isInRaid() ? GameWorld.MainPlayer : null;
        public static List<Player> AllPlayers => isInRaid() ? GetAllPlayers() : new();

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

        public static Dictionary<Weapon, MagAndAmmo> AmmoRegistry => Patch_FirearmController_InitiateShot.AmmoRegistry;

        public static void Notify(string msg) => NotificationManagerClass.DisplayMessageNotification(msg);
        public static void NotifyLong(string msg) => NotificationManagerClass.DisplayMessageNotification(msg, EFT.Communications.ENotificationDurationType.Long);

        public static void Log(string msg) => Plugin.Logger.LogInfo(msg);

        public static void PlayMusic(MusicEvent musicEvent) => MusicManager.Instance?.PlayEvent(musicEvent);
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
            return GameWorld.AllAlivePlayersList.FirstOrDefault(p => p.Id == playerId); ;
        }

        public static PlayerScore GetPlayerScore(int playerId)
        {
            if (!Singleton<ArenaController>.Instantiated) return null;

            Arena.session.scoreboard.TryGetValue(playerId, out var playerScore);
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
    }
}