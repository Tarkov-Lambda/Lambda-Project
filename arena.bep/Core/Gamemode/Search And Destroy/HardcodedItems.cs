using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using ifp.arena.bep.networking; // Assuming PlayerKilledPacket is here
using System.Collections.Generic;
using UnityEngine;
using EFT.InventoryLogic;
using System;

namespace ifp.arena.bep.Core.Economy
{
    public static class BuyMenu
    {
        public static List<BuyCategory> buyCategories = new();
        static BuyMenu()
        {
            buyCategories.Add(new BuyCategory
            {
                name = "Equipment",
                items = [
                    new ShopItem
                    {
                        bsgId = "656fa8d700d62bcd2e024084", // Kevlar - Locust Tier 5
                        price = 650
                    },
                    new ShopItem
                    {
                        bsgId = "5b40e1525acfc4771e1c6611", // Helmet
                        price = 350
                    },
                    new ShopItem
                    {
                        bsgId = "544fb5454bdc2df8738b456a", // Defuse Kit
                        price = 400,
                        faction = Faction.CT
                    },
                ]
            });
            buyCategories.Add(new BuyCategory
            {
                name = "Pistols",
                items = [
                    new ShopItem
                    {
                        bsgId = "5a7ae0c351dfba0017554310", // Glock 17 9x19
                        ammoId = "5c925fa22e221601da359b7b", // AP 6.3
                        price = 200,
                        faction = Faction.T,
                    },
                    new ShopItem
                    {
                        bsgId = "602a9740da11d6478d5a06dc", // PL-15 9x19
                        ammoId = "5c925fa22e221601da359b7b", // AP 6.3
                        price = 200,
                        faction = Faction.CT,
                    },
                    new ShopItem
                    {
                        bsgId = "6193a720f8ee7e52e42109ed", // USP .45 ACP
                        ammoId = "5efb0cabfb3e451d70735af5", // ACP AP
                        price = 300,
                    },
                    new ShopItem
                    {
                        bsgId = "5d67abc1a4b93614ec50137f", // Five-Seven FDE 5.7
                        ammoId = "5cc80f38e4a949001152b560", // SS190
                        price = 500,
                        faction = Faction.T,
                    },
                    new ShopItem
                    {
                        bsgId = "5d3eb3b0a4b93615055e84d2", // Five-Seven 5.7
                        ammoId = "5cc80f38e4a949001152b560", // SS190
                        price = 500,
                        faction = Faction.CT,
                    },
                    new ShopItem
                    {
                        bsgId = "633ec6ee025b096d320a3b15", // RSH12B
                        ammoId = "5cadf6eeae921500134b2799", // PS12B
                        price = 400,
                    },
                    new ShopItem
                    {
                        bsgId = "669fa3d876116c89840b1217", // Deagle
                        ammoId = "668fe62ac62660a5d8071446", // .50 AE FMJ
                        price = 700,
                    },
                ]
            });

            buyCategories.Add(new BuyCategory
            {
                name = "Mid-Tier",
                items = [
                    new ShopItem
                    {
                        bsgId = "5926bb2186f7744b1c6c6e60", // MP5
                        ammoId = "5c925fa22e221601da359b7b", // AP 6.3
                        price = 1500,
                    },
                    new ShopItem
                    {
                        bsgId = "5fc3e272f8b6a877a729eac5", // UMP
                        ammoId = "5efb0cabfb3e451d70735af5", // ACP AP
                        price = 1050,
                    },
                    new ShopItem
                    {
                        bsgId = "57c44b372459772d2b39b8ce", // AS VAL
                        ammoId = ,
                        price = 200,
                    },
                    new ShopItem
                    {
                        bsgId = ,
                        ammoId = ,
                        price = 200,
                    },
                    new ShopItem
                    {
                        bsgId = ,
                        ammoId = ,
                        price = 200,
                    }
                ]
            });
            buyCategories.Add(new BuyCategory
            {

            });
            buyCategories.Add(new BuyCategory
            {

            });
        }
    }

    public struct BuyCategory
    {
        public string name;
        public ShopItem[] items;
    }

    public struct ShopItem
    {
        // if the item is a weapon
        // we will be finding the actual build using PresetUtils
        public string bsgId;
        public string ammoId;
        public int price;
        public Faction faction; // Only shown for this faction
    }
}