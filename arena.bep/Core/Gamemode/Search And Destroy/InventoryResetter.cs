using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.UI;
using ifp.arena.shared;

namespace ifp.arena.bep.Core.Gamemode;

public static class InventoryResetter
{
    public static bool IsResetting { get; private set; }

    public static PistolItemClass GetDefaultPistol()
    {
        foreach (var category in BuyMenuSelection.buyCategories)
        {
            foreach (var shopItem in category.items)
            {
                if (string.IsNullOrEmpty(shopItem.ammoId))
                    continue;

                var immutable = Singleton<PresetItemsCache>.Instance.GetPresetItem(shopItem.bsgId);
                if (immutable is not PistolItemClass pistolItem)
                    continue;

                if (shopItem.faction == H.MainPlayerScore.Faction || shopItem.faction == Faction.None)
                    return pistolItem;
            }
        }

        return null;
    }

    public static AssaultCarbineItemClass GetFirstAssaultCarbineItem()
    {
        foreach (var category in BuyMenuSelection.buyCategories)
        {
            foreach (var shopItem in category.items)
            {
                if (string.IsNullOrEmpty(shopItem.ammoId))
                    continue;

                var immutable = Singleton<PresetItemsCache>.Instance.GetPresetItem(shopItem.bsgId);
                if (immutable is not AssaultCarbineItemClass assaultCarbine)
                    continue;

                if (shopItem.faction == H.MainPlayerScore.Faction || shopItem.faction == Faction.None)
                    return assaultCarbine;
            }
        }

        return null;
    }

    public static void AddItem(ref List<Item> itemList, Item item)
    {
        if (itemList.Contains(item)) return;
        if (item == null) return;
        D.LogInventory($"Adding {item.LocalizedName()} ({item.Id}) to removal list");
        itemList.Add(item);
    }

    public static void AddRange(ref List<Item> itemList, IEnumerable<Item> itemCollection)
    {
        foreach (Item item in itemCollection)
        {
            D.LogInventory($"Adding {item.LocalizedName()} ({item.Id}) to removal list");
        }
        itemList.AddRange(itemCollection);
    }


    public static async UniTask SoftReset()
    {
        IsResetting = true;
        try
        {
            List<Item> itemsToRemove = [];

            H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
            // List<MagazineItemClass> allMags = H.MainPlayer.GetVestAndPocketGridItems<MagazineItemClass>().ToList();

            var secondPrimaryWeapon = H.MainPlayer.GetSlotItem(EquipmentSlot.SecondPrimaryWeapon);
            AddItem(ref itemsToRemove, secondPrimaryWeapon);

            await H.MainPlayer.TryPopItem(secondPrimaryWeapon);

            AddRange(ref itemsToRemove, H.MainPlayer.GetNonMatchingMags());

            await H.MainPlayer.TryPopItems(itemsToRemove);
        }
        finally
        {
            IsResetting = false;
        }
    }

    public static async UniTask HardReset()
    {
        IsResetting = true;
        try
        {
            H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();


            List<Item> itemsToRemove = [];

            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (slot
                is EquipmentSlot.ArmBand
                or EquipmentSlot.Dogtag
                or EquipmentSlot.Scabbard
                or EquipmentSlot.SecuredContainer) continue;

                var currentItem = H.MainPlayer.GetSlotItem(slot);
                AddItem(ref itemsToRemove, currentItem);
            }

            await H.MainPlayer.TryPopItems(itemsToRemove);


            List<Item> pocketItemsToRemove = H.MainPlayer.GetVestAndPocketGridItems<Item>().ToList();

            await H.MainPlayer.TryPopItems(pocketItemsToRemove);

            // GIVING
            foreach (var kvp in DefaultEquipmentManager.Instance.RecordedItems)
            {
                var currentItem = H.MainPlayer.GetSlotItem(kvp.Key);
                if (kvp.Value != null && (currentItem == null || currentItem.TemplateId != kvp.Value.TemplateId))
                {
                    await UniTask.Delay(25);
                    await IU.ClientRequestGiveItem(kvp.Value);
                }
            }

            await IU.ClientRequestGiveItem(GetDefaultPistol());
        }
        finally
        {
            IsResetting = false;
        }
    }
}
