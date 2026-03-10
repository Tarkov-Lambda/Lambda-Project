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

        public static bool TryGetItemData(string bsgId, out ShopItem itemData)
        {
            foreach (var category in buyCategories)
            {
                foreach (var item in category.items)
                {
                    if (item.bsgId == bsgId)
                    {
                        itemData = item;
                        return true;
                    }
                }
            }
            itemData = new ShopItem();
            return false;
        }

        static BuyMenu()
        {
            buyCategories.Add(new BuyCategory
            {
                name = "Equipment",
                items = [
                    new ShopItem
                    {
                        bsgId = "655746010177119f4a097ff7", // Kevlar - SAPI level 3+ Ceramic
                        price = 650
                    },
                    new ShopItem
                    {
                        bsgId = "5b40e1525acfc4771e1c6611", // Helmet - ULACH
                        price = 350
                    },
                    new ShopItem
                    {
                        bsgId = "544fb5454bdc2df8738b456a", // Defuse Kit - Multitool
                        price = 400,
                        faction = Faction.CT
                    },
                    new ShopItem
                    {
                        bsgId = "628bc7fb408e2b2e9c0801b1", // Bomb - Mystery Ranch NICE COMM 3 BVS frame system (Coyote)
                        price = 0,
                        faction = Faction.T
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
                        bsgId = "633ec7c2a6918cb895019c6c", // Rsh-12
                        ammoId = "5cadf6eeae921500134b2799", // PS12B
                        price = 500,
                    },
                    new ShopItem
                    {
                        bsgId = "669fa39b48fc9f8db6035a0c", // Deagle
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
                        bsgId = "5fc3e272f8b6a877a729eac5", // UMP
                        ammoId = "5efb0cabfb3e451d70735af5", // ACP AP
                        price = 1050,
                    },
                    new ShopItem
                    {
                        bsgId = "5926bb2186f7744b1c6c6e60", // MP5
                        ammoId = "5c925fa22e221601da359b7b", // AP 6.3
                        price = 1200,
                    },
                    new ShopItem
                    {
                        bsgId = "628a60ae6b1d481ff772e9c8", // RD-704
                        ammoId = "5656d7c34bdc2d9d198b4587", // PS
                        price = 1500,
                        faction = Faction.T
                    },
                    new ShopItem
                    {
                        bsgId = "5c488a752e221602b412af63", // MDR
                        ammoId = "5c488a752e221602b412af63", // SOST
                        price = 1500,
                        faction = Faction.CT
                    },
                    new ShopItem
                    {
                        bsgId = "5e00903ae9dc277128008b87", // MP9
                        ammoId = "5c925fa22e221601da359b7b", // AP 6.3
                        price = 1700,
                    },
                    new ShopItem
                    {
                        bsgId = "57c44b372459772d2b39b8ce", // AS VAL
                        ammoId = "57a0dfb82459774d3078b56c", // SP-5
                        price = 1800,
                    },
                ]
            });
            buyCategories.Add(new BuyCategory
            {
                name = "Rifles",
                items = [
                    new ShopItem
                    {
                        bsgId = "5a367e5dc4a282000e49738f", // RSASS
                        ammoId = "5e023e53d4353e3302577c4c", // BCP
                        price = 1800,
                        faction = Faction.T
                    },
                    new ShopItem
                    {
                        bsgId = "5a367e5dc4a282000e49738f", // SR-25
                        ammoId = "5e023e53d4353e3302577c4c", // BCP
                        price = 2050,
                        faction = Faction.CT
                    },
                    new ShopItem
                    {
                        bsgId = "6499849fc93611967b034949", // AK-12
                        ammoId = "61962b617c6c7b169525f168", // 7N40
                        price = 2700,
                        faction =  Faction.T
                    },
                    new ShopItem
                    {
                        bsgId = "5447a9cd4bdc2dbd208b4567", // M4A1
                        ammoId = "657024ecc5d7d4cb4d07856d", // M856A1
                        price = 3100,
                        faction =  Faction.CT
                    },
                    new ShopItem
                    {
                        bsgId = "5b0bbe4e5acfc40dc528a72d", // SA58
                        ammoId = "5e023e53d4353e3302577c4c", // M80
                        price = 3000,
                        faction = Faction.T
                    },
                    new ShopItem
                    {
                        bsgId = "606587252535c57a13424cfd", // Mutant
                        ammoId = "59e0d99486f7744a32234762", // BP
                        price = 3300,
                        faction = Faction.CT
                    },
                    new ShopItem
                    {
                        bsgId = "65290f395ae2ae97b80fdf2d", // SPEAR
                        ammoId = "6529302b8c26af6326029fb7", // SIG FMJ
                        price = 4000,
                    },
                    new ShopItem
                    {
                        bsgId = "673cab3e03c6a20581028bc1", // TRG
                        ammoId = "6489848173c462723909a14b", // AP
                        price = 4750,
                    }
                ]
            });
            buyCategories.Add(new BuyCategory
            {
                name = "Utility",
                items = [
                    new ShopItem
                    {
                        bsgId = "619256e5f8af2c1a4e1f5d92", // Flashbang
                        price = 200,
                        maxQuantity = 2,
                        maxBuy = 2
                        // faction = Faction.CT
                    },
                    new ShopItem
                    {
                        bsgId = "617aa4dd8166f034d57de9c5", // Smoke
                        price = 300,
                        maxQuantity = 3,
                        maxBuy = 1
                    },
                    new ShopItem
                    {
                        bsgId = "66dae7cbeb28f0f96809f325", // V40
                        price = 300,
                        maxQuantity = 2,
                        maxBuy = 1
                    },
                ]
            });
        }
    }
}