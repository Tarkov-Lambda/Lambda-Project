using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking; // Assuming PlayerKilledPacket is here
using System.Collections.Generic;
using UnityEngine;
using EFT.InventoryLogic;
using System;
using Newtonsoft.Json;
using HarmonyLib;
using System.IO;
using ifp.arena.shared.Models;

namespace ifp.arena.bep.Core.Economy
{
    public static class BuyMenu
    {
        public static string EconomyDataPath = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "json", "Economy.json");

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
                    else if (item.ammoId == bsgId)
                    {
                        itemData = item;
                        return true;
                    }
                }
            }
            itemData = new ShopItem();
            return false;
        }

        public static void LoadItems(string json)
        {
            buyCategories = JsonConvert.DeserializeObject<List<BuyCategory>>(json);
        }

        static BuyMenu()
        {
            LoadItems(File.ReadAllText(EconomyDataPath));
        }
    }
}