using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using ifp.arena.shared.Models;

namespace ifp.arena.bep.Core.Economy;

public static class BuyMenuSelection
{
    private readonly static string EconomyDataPath = Path.Combine(Plugin.pathToConfigs, "Economy.jsonc");

    public static List<BuyCategory> buyCategories = new();

    static BuyMenuSelection()
    {
        LoadItems(File.ReadAllText(EconomyDataPath));
    }
    private static void LoadItems(string json)
    {
        buyCategories = JsonConvert.DeserializeObject<List<BuyCategory>>(json);
    }

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
}
