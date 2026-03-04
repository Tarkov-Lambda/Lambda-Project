

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;

namespace ifp.arena.bep.Core
{
    // Helper class for singleton refences & helper functions
    public static class H
    {
        public static GameWorld GameWorld => Singleton<GameWorld>.Instance;
        public static IFikaNetworkManager FikaNet => Singleton<IFikaNetworkManager>.Instance;
        public static Player MainPlayer => isInRaid() ? GameWorld.MainPlayer : null;

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
            return GameWorld.AllAlivePlayersList;
        }

        public static bool isInRaid()
        {
            return GameWorld != null && GameWorld is not HideoutGameWorld;
        }
    }

    public static class PlayerUtils
    {
        public static void ApplyPainkiller()
        {
            if (!H.isInRaid()) return;

            var healthController = H.MainPlayer.ActiveHealthController;

            Type painKillerType = AccessTools.TypeByName("EFT.HealthSystem.ActiveHealthController+PainKiller");

            var isPainkillerAlreadyActive = healthController.GetAllEffects().FirstOrDefault(effect => effect.GetType() == painKillerType && effect.BodyPart == EBodyPart.Head);

            if (isPainkillerAlreadyActive != null) return;
            healthController.DoPainKiller();
        }

        public static void RepairMe()
        {
            foreach (var slot in H.MainPlayer.Inventory.Equipment.AllSlots)
            {
                foreach (var item in slot.Items)
                {
                    RepairItem(item);
                }
            }

        }

        private static void RepairItem(Item item)
        {
            if (item is CompoundItem compoundItem)
            {
                foreach (var slot in compoundItem.AllSlots)
                {
                    foreach (var childItem in slot.Items)
                    {
                        if (childItem is ArmoredEquipmentItemClass armoredEquipmentItemClass)
                        {
                            armoredEquipmentItemClass.Repairable.Durability = armoredEquipmentItemClass.Repairable.MaxDurability;
                        }
                    }
                }
            }
            if (item is Weapon weapon)
            {
                weapon.Repairable.Durability = 100;
            }
        }
    }
}