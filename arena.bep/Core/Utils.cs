

using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;

namespace ifp.arena.bep.Core
{
    // vague posting
    public static class H
    {
        public static GameWorld gameWorld => Singleton<GameWorld>.Instance;

        // vague posting again
        public static ArenaController game => Singleton<ArenaController>.Instance;
        public static SessionInfo session => Singleton<ArenaController>.Instance.session;
        public static Dictionary<int, PlayerScore> scoreboard => Singleton<ArenaController>.Instance.session.scoreboard;
        public static void Notify(string msg) => NotificationManagerClass.DisplayMessageNotification(msg);


        // bro thinks he's the main character
        public static Player GetMainPlayer()
        {
            NotificationManagerClass.DisplayMessageNotification($"{isGameWorldActive()}");

            if (!isGameWorldActive()) return null;
            return gameWorld.MainPlayer;
        }

        public static Player GetPlayer(int playerId)
        {
            if (!isGameWorldActive()) return null;
            return gameWorld.AllAlivePlayersList.FirstOrDefault(p => p.Id == playerId); ;
        }

        public static PlayerScore GetPlayerScore(int playerId)
        {
            if (!Singleton<ArenaController>.Instantiated) return null;

            game.session.scoreboard.TryGetValue(playerId, out var playerScore);
            return playerScore;
        }

        public static List<Player> GetAllPlayers()
        {
            if (!isGameWorldActive()) return null;
            return Singleton<GameWorld>.Instance.AllAlivePlayersList;
        }

        public static bool isGameWorldActive()
        {
            return Singleton<GameWorld>.Instantiated;
        }
    }
}