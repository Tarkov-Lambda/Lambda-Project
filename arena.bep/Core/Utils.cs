

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using Fika.Core.Networking;
using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Patches.Tarkov;
using UnityEngine;

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

        public static Dictionary<Weapon, MagAndAmmo> AmmoRegistry => Patch_FirearmController_InitiateShot.AmmoRegistry;

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
        // Permament painkiller that gets applied at the start of the raid
        public static void ApplyPainkiller()
        {
            if (!H.isInRaid()) return;

            var healthController = H.MainPlayer.ActiveHealthController;

            Type painKillerType = AccessTools.TypeByName("EFT.HealthSystem.ActiveHealthController+PainKiller");

            var isPainkillerAlreadyActive = healthController.GetAllEffects().FirstOrDefault(effect => effect.GetType() == painKillerType && effect.BodyPart == EBodyPart.Head);

            if (isPainkillerAlreadyActive != null) return;
            healthController.DoPainKiller();
        }

        // Repl all equipped weapons/armor
        public static void ReplenishMe()
        {
            foreach (var slot in H.MainPlayer.Equipment.AllSlots)
            {
                foreach (var item in slot.Items)
                {
                    RepairItem(item);

                    if (item is Weapon weapon)
                    {
                        ReplenishGun(weapon, H.AmmoRegistry[weapon].ammo);

                        Slot vest = H.MainPlayer.Equipment.GetSlot(EquipmentSlot.TacticalVest);
                        if (vest.ContainedItem is CompoundItem vestCompoundItem)
                        {
                            foreach (var grid in vestCompoundItem.Grids)
                            {
                                var containerItem = grid.Items.FirstOrDefault();
                                if (containerItem != null && containerItem is MagazineItemClass magazineItem)
                                {
                                    ReplenishMagazine(magazineItem, H.AmmoRegistry[weapon].ammo);
                                }
                            }
                        }
                        // if (TryCreateItem(weapon.GetCurrentMagazine().TemplateId, out Item newMag))
                        // {
                        //     ItemContextAbstractClass baseContext = new SimpleItemContext(newMag, EItemViewType.Inventory);
                        //     ItemContextClass itemContext = new ItemContextClass(baseContext, ItemRotation.Vertical);

                        //     Plugin.Logger.LogInfo(ItemUiContext.Instance.QuickFindAppropriatePlace(itemContext, H.MainPlayer.InventoryController));
                        // }
                    }
                }
            }

        }

        public class SimpleItemContext : ItemContextAbstractClass
        {
            public SimpleItemContext(Item item, EItemViewType viewType) : base(item, viewType, null) { }

            public override ItemContextAbstractClass CreateChild(Item item)
            {
                return new SimpleItemContext(item, this.ViewType);
            }
        }

        // This method replenishes gun (ReplenishGun) using weapon and ammo parameters <- TRUTH NUKE (truth nuke)
        public static void ReplenishGun(Weapon weapon, AmmoItemClass ammo)
        {
            var magazine = weapon.GetCurrentMagazine();

            if (magazine != null)
            {
                ReplenishMagazine(magazine, ammo);
                return;
            }

            foreach (var chamber in weapon.Chambers)
            {
                if (chamber.ContainedItem == null)
                {
                    if (TryCreateItem(ammo.TemplateId, out Item newItem))
                    {
                        chamber.AddWithoutRestrictions(newItem);
                    }
                }
            }
        }

        public static void ReplenishMagazine(MagazineItemClass magazine, AmmoItemClass ammo)
        {
            // Handle cylinder magazines
            if (magazine is CylinderMagazineItemClass cylinder)
            {
                foreach (var camora in cylinder.Camoras)
                {
                    if (camora.ContainedItem == null)
                    {
                        if (TryCreateItem(ammo.TemplateId, out Item newItem))
                        {
                            camora.AddWithoutRestrictions(newItem);
                        }
                    }
                }

                return;
            }

            if (magazine.Cartridges != null)
            {
                var topAmmoItem = magazine.Cartridges.Items.LastOrDefault();

                if (topAmmoItem != null)
                {
                    topAmmoItem.StackObjectsCount =
                        Math.Min(topAmmoItem.Template.StackMaxSize, magazine.MaxCount);
                }
                else
                {
                    if (TryCreateItem(ammo.TemplateId, out Item newItem))
                    {
                        newItem.StackObjectsCount = magazine.MaxCount;
                        magazine.Cartridges.Add(newItem, simulate: false);
                    }
                }
            }
        }

        private static bool TryCreateItem(string templateId, out Item newItem)
        {
            newItem = null;

            if (!Singleton<ItemFactoryClass>.Instantiated)
                return false;

            if (!Singleton<ItemFactoryClass>.Instance.ItemTemplates.ContainsKey(templateId))
                return false;

            newItem = Singleton<ItemFactoryClass>.Instance.CreateItem(MongoID.Generate(), templateId, itemDiff: null);

            return newItem != null;
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
            else if (item is Weapon weapon)
            {
                weapon.Repairable.Durability = 100;
            }
        }
    }
}