

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using Fika.Core.Networking;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;

namespace ifp.arena.bep.Core
{
    // vague posting
    public static class H
    {
        public static GameWorld GameWorld => Singleton<GameWorld>.Instance;
        public static IFikaNetworkManager FikaNet => H.FikaNet;

        // vague posting again
        public static ArenaController Arena => Singleton<ArenaController>.Instance;
        public static SessionInfo Session => Singleton<ArenaController>.Instance.session;
        public static Dictionary<int, PlayerScore> Scoreboard => Singleton<ArenaController>.Instance.session.scoreboard;


        public static void Notify(string msg) => NotificationManagerClass.DisplayMessageNotification(msg);


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

        public static List<Player> GetAllPlayers()
        {
            if (!isInRaid()) return null;
            return H.GameWorld.AllAlivePlayersList;
        }

        public static bool isInRaid()
        {
            return GameWorld != null && GameWorld is not HideoutGameWorld;
        }

        public static async Task Delay(int ms)
        {
            await Task.Delay(ms);
        }
    }
}