using EFT.InventoryLogic;
using ifp.arena.bep.Core;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ifp.arena.bep.Core.Gamemode
{
    public static class Purchasing
    {
        public static int GetItemPrice(Item item)
        {
            int price = 0;
            switch (item)
            {
                case Weapon:
                    price = GetWeaponPrice(item as Weapon);
                    break;
                // Plates that will be put in the player's rig
                case ArmorPlateItemClass:
                    price = 650;
                    break;
                // Headwear
                case ArmoredEquipmentItemClass:
                    price = GetWeaponPrice(item as Weapon);
                    break;
            }
            return price;
        }

        public static int GetWeaponPrice(Weapon weapon)
        {
            int price = 0;
            switch (weapon)
            {
                case PistolItemClass:
                    price = 800;
                    break;
                case AssaultRifleItemClass:
                    price = 800;
                    break;
                case MarksmanRifleItemClass:
                    price = 2500;
                    break;
                case SniperRifleItemClass:
                    price = 800;
                    break;
            }
            return price;
        }

        public static bool CanAfford(Item item)
        {
            return H.MainPlayerScore.money >= GetItemPrice(item);
        }

        public static void BuyItem(Item item)
        {
            ItemsUtils.GiveItem(item, H.MainPlayer);
            H.MainPlayerScore.money -= GetItemPrice(item);
        }
    }
}
//