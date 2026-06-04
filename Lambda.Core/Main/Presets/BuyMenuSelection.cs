using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using Lambda.Shared.Models;
using EFT.InventoryLogic;

namespace Lambda.Core.Main.Economy;

public static class BuyMenuSelection
{
    private readonly static string EconomyDataPath = Path.Combine(LambdaPlugin.pathToConfigs, "Economy.jsonc");

    public static List<BuyCategory> buyCategories = new();

    static BuyMenuSelection()
    {
        LoadItems(File.ReadAllText(EconomyDataPath));
    }

    private static void LoadItems(string json)
    {
        buyCategories = JsonConvert.DeserializeObject<List<BuyCategory>>(json);
    }

    // пиздец буквально просто хуйню пишу
    public static bool TryGetItemData(string bsgId, out ShopItem itemData)
    {
        var result = TryGetItemData(buyCategories, bsgId, out ShopItem itemDataInside);
        itemData = itemDataInside;
        return result;
    }

    public static bool TryGetItemData(this List<BuyCategory> buyMenu, string bsgId, out ShopItem itemData)
    {
        foreach (var category in buyMenu)
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

    public static List<string> GetAllItemBsgId() => GetAllItemBsgId(buyCategories);

    public static List<string> GetAllItemBsgId(this List<BuyCategory> buyMenu)
    {
        List<string> AllItemBsgIds = [];

        foreach (var shopItem in buyMenu.GetAllShopItems())
        {
            AllItemBsgIds.Add(shopItem.bsgId);
        }

        return AllItemBsgIds;
    }

    public static List<ShopItem> GetAllShopItems() => GetAllShopItems(buyCategories);

    public static List<ShopItem> GetAllShopItems(this List<BuyCategory> buyMenu)
    {
        List<ShopItem> AllItems = [];

        foreach (var category in buyMenu)
        {
            foreach (var item in category.items)
            {
                AllItems.Add(item);
            }
        }

        return AllItems;
    }
}
