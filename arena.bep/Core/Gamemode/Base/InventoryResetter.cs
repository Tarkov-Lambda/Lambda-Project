using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.UI;
using ifp.arena.bep.networking;
using ifp.arena.shared;

namespace ifp.arena.bep.Core.Gamemode;

public static class InventoryResetter
{
    public static bool IsResetting { get; private set; }

    public static PistolItemClass GetDefaultPistol(this PlayerScore playerScore)
    {
        foreach (var category in BuyMenuSelection.buyCategories)
        {
            foreach (var shopItem in category.items)
            {
                if (string.IsNullOrEmpty(shopItem.ammoId))
                    continue;

                var immutable = playerScore.BuySelection[shopItem];
                if (immutable is not PistolItemClass pistolItem)
                    continue;

                if (shopItem.faction == playerScore.Faction || shopItem.faction == Faction.None)
                    return pistolItem;
            }
        }

        return null;
    }

    public static SniperRifleItemClass GetFirstSniperRifleItem(this PlayerScore playerScore)
    {
        foreach (var category in BuyMenuSelection.buyCategories)
        {
            foreach (var shopItem in category.items)
            {
                if (string.IsNullOrEmpty(shopItem.ammoId))
                    continue;

                var immutable = Singleton<PresetItemsCache>.Instance.GetPresetItem(shopItem.bsgId);
                if (immutable is not SniperRifleItemClass assaultCarbine)
                    continue;

                if (shopItem.faction == playerScore.Faction || shopItem.faction == Faction.None)
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


    public static void SoftReset(this Player player)
    {
        List<Item> itemsToRemove = [];

        var secondPrimaryWeapon = player.GetSlotItem(EquipmentSlot.SecondPrimaryWeapon);
        AddItem(ref itemsToRemove, secondPrimaryWeapon);

        var backpack = player.GetSlotItem(EquipmentSlot.Backpack);
        AddItem(ref itemsToRemove, backpack);

        AddRange(ref itemsToRemove, player.GetNonMatchingMags());

        foreach (var itemToRemove in itemsToRemove)
        {
            itemToRemove.CurrentAddress.RemoveWithoutRestrictions(itemToRemove);
        }

        var firstPrimaryWeapon = player.GetSlotItem(EquipmentSlot.FirstPrimaryWeapon) as Weapon;
        if (firstPrimaryWeapon != null)
        {
            RU.SetupWeaponImmediate(firstPrimaryWeapon, player);
        }

        var pistol = player.GetSlotItem(EquipmentSlot.SecondPrimaryWeapon) as Weapon;
        if (pistol == null)
        {
            PistolItemClass defaultPistol = GetDefaultPistol(player.GetScore()).CloneItem();
            var pistolPlacement = AU.GetItemPlacement(defaultPistol, player);
            pistolPlacement.Address.AddWithoutRestrictions(defaultPistol);
            pistol = defaultPistol;
        }

        RU.SetupWeaponImmediate(pistol, player);

        if (H.IsNightTime)
        {
            // item utilities automatically adds NVGs to headwear if it's night time
            var Headwear = player.GetSlotItem(EquipmentSlot.Headwear);
            if (Headwear != null && Headwear.TemplateId != Hardcode.STRAP_NVG)
            {
                var HelmetWithNVGs = PresetItemsCache.Instance.GetPresetItem(Headwear.TemplateId).CloneItem() as HeadwearItemClass;
                IU.AttachNightVisionIfNeeded(HelmetWithNVGs);
            }
            else
            {
                var NVGStrap = PresetItemsCache.Instance.GetPresetItem(Hardcode.STRAP_NVG).CloneItem() as HeadwearItemClass;
                IU.AttachNightVisionIfNeeded(NVGStrap);

                var placement = AU.GetItemPlacement(NVGStrap, player);

                placement.Address.AddWithoutRestrictions(NVGStrap);
            }
        }
    }

    public static async void HardReset(this Player player)
    {
        List<Item> itemsToRemove = [];

        foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
        {
            if (slot
            is EquipmentSlot.ArmBand
            or EquipmentSlot.Dogtag
            or EquipmentSlot.Scabbard
            or EquipmentSlot.SecuredContainer
            or EquipmentSlot.Pockets) continue;

            var currentItem = player.GetSlotItem(slot);
            AddItem(ref itemsToRemove, currentItem);
        }

        AddRange(ref itemsToRemove, player.GetVestAndPocketGridItems<Item>().ToList());

        foreach (var itemToRemove in itemsToRemove)
        {
            itemToRemove.CurrentAddress.RemoveWithoutRestrictions(itemToRemove);
        }

        // GIVING
        foreach (var kvp in H.GetPlayerScore(player.Id).RecordedItems)
        {
            if (kvp.Value == null) continue;

            var currentItem = player.GetSlotItem(kvp.Key);
            if (currentItem == null || currentItem.TemplateId != kvp.Value.TemplateId)
            {
                var clonedItem = kvp.Value.CloneItem();
                var placement = AU.GetItemPlacement(clonedItem, player);

                IU.StripArmorPlatesIfNeeded(clonedItem);

                placement.Address.AddWithoutRestrictions(clonedItem);
            }
        }

        // NVG for night time
        if (H.IsNightTime)
        {
            var NVGStrap = PresetItemsCache.Instance.GetPresetItem(Hardcode.STRAP_NVG).CloneItem() as HeadwearItemClass;
            IU.AttachNightVisionIfNeeded(NVGStrap);

            var placement = AU.GetItemPlacement(NVGStrap, player);

            placement.Address.AddWithoutRestrictions(NVGStrap);
        }

        // Default Pistol
        PistolItemClass defaultPistol = GetDefaultPistol(player.GetScore()).CloneItem();

        var pistolPlacement = AU.GetItemPlacement(defaultPistol, player);

        pistolPlacement.Address.AddWithoutRestrictions(defaultPistol);

        RU.SetupWeaponImmediate(defaultPistol, player);
    }
}