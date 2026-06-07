using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main.Economy;
using Lambda.Core.Main.UI;

#pragma warning disable IDE0019

namespace Lambda.Core.Main.Gamemode;

public class BaseInventoryManager : IInventoryManager
{
    public static void EnforceOnePrimaryWeaponAtMost(Player player, ref List<Item> itemsToRemove)
    {
        var firstPrimaryWeapon = player.GetSlotItem(EquipmentSlot.FirstPrimaryWeapon) as Weapon;
        var secondPrimaryWeapon = player.GetSlotItem(EquipmentSlot.SecondPrimaryWeapon) as Weapon;

        // Make sure that the player only has one primary, but also make sure that we only ever delete one at most
        if (secondPrimaryWeapon != null)
        {
            if (firstPrimaryWeapon != null)
            {
                AddItem(ref itemsToRemove, secondPrimaryWeapon);
            }
            else
            {
                secondPrimaryWeapon.CurrentAddress.RemoveWithoutRestrictions(secondPrimaryWeapon);
                player.Equipment.GetSlot(EquipmentSlot.FirstPrimaryWeapon).AddWithoutRestrictions(secondPrimaryWeapon);
                firstPrimaryWeapon = secondPrimaryWeapon;
            }
        }

        AddRange(ref itemsToRemove, player.GetNonMatchingMags());

        if (firstPrimaryWeapon != null)
        {
            RU.SetupWeaponLocally(firstPrimaryWeapon, player);
            firstPrimaryWeapon.MalfState.ChangeStateSilent(Weapon.EMalfunctionState.None);
        }
    }

    public virtual void Replenish(Player player)
    {
        List<Item> itemsToRemove = [];

        EnforceOnePrimaryWeaponAtMost(player, ref itemsToRemove);

        var backpack = player.GetSlotItem(EquipmentSlot.Backpack);
        AddItem(ref itemsToRemove, backpack);

        foreach (var itemToRemove in itemsToRemove)
        {
            itemToRemove.CurrentAddress.RemoveWithoutRestrictions(itemToRemove);
        }

        GiveDefaultEquipment(player);

        Slot tacRigSlot = player.GetSlot(EquipmentSlot.TacticalVest);
        if (tacRigSlot.ContainedItem == null)
        {
            IU.TryCreateItem(Hardcode.DEFAULT_TAC_RIG, out Item BlackrockRig);
            tacRigSlot.CreateItemAddress().AddWithoutRestrictions(BlackrockRig.CloneItem());
        }

        if (H.IsNightTime)
        {
            // item utilities automatically adds NVGs to headwear if it's night time
            var Headwear = player.GetSlotItem(EquipmentSlot.Headwear);
            if (Headwear != null && Headwear.TemplateId == Hardcode.HELMET)
            {
                IU.AttachNightVisionIfNeeded(Headwear as HeadwearItemClass);
            }
            else
            {
                var NVGStrap = PresetItemsCache.Instance.GetPresetItem(Hardcode.STRAP_NVG).CloneItem() as HeadwearItemClass;
                IU.AttachNightVisionIfNeeded(NVGStrap);

                var placement = AU.GetItemPlacement(NVGStrap, player);

                placement.Address.AddWithoutRestrictions(NVGStrap);
            }
        }

        RU.Replenish(player, false);

        IU.AddArmbandIfNeeded(player);
    }

    public virtual void HardReset(Player player)
    {
        List<Item> itemsToRemove = [];

        foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
        {
            if (slot is EquipmentSlot.Pockets or EquipmentSlot.Dogtag) continue;

            var currentItem = player.GetSlotItem(slot);
            AddItem(ref itemsToRemove, currentItem);
        }

        AddRange(ref itemsToRemove, player.GetVestAndPocketGridItems<Item>());

        itemsToRemove.Reverse();

        foreach (var itemToRemove in itemsToRemove)
        {
            itemToRemove.CurrentAddress.RemoveWithoutRestrictions(itemToRemove);
        }

        GiveDefaultEquipment(player);

        // In DefaultEquipmentManager, we save the equipment as is
        // because of this, the player will retain whatever was in the rig at that time.
        // we have to manually remove it all here every time
        List<Item> RigItems = new();
        AddRange(ref RigItems, player.GetVestAndPocketGridItems<Item>());
        foreach (var itemToRemove in RigItems)
        {
            itemToRemove.CurrentAddress.RemoveWithoutRestrictions(itemToRemove);
        }

        if (H.IsNightTime)
        {
            var NVGStrap = PresetItemsCache.Instance.GetPresetItem(Hardcode.STRAP_NVG).CloneItem() as HeadwearItemClass;
            IU.AttachNightVisionIfNeeded(NVGStrap);

            var placement = AU.GetItemPlacement(NVGStrap, player);

            placement.Address.AddWithoutRestrictions(NVGStrap);
        }

        IU.AddArmbandIfNeeded(player);
    }

    // TODO: add check for when we are replenishing a player to avoid collision between an armor vest and armored tac rig
    public static void GiveDefaultEquipment(Player player)
    {
        foreach (var kvp in player.Context.DefaultEquipment)
        {
            Item defaultItem = kvp.Value;
            if (defaultItem == null) continue;

            var currentItem = player.GetSlotItem(kvp.Key);
            if (currentItem == null)
            {
                var clonedItem = defaultItem.CloneItem();
                var placement = AU.GetItemPlacement(clonedItem, player);

                IU.StripArmorPlatesIfNeeded(clonedItem);

                var addResult = placement.Address.Add(clonedItem, simulate: true);
                if (addResult.Failed) continue;

                placement.Address.AddWithoutRestrictions(clonedItem);
            }
        }
    }

    public static PistolItemClass GetDefaultPistol(PlayerContext pContext)
    {
        foreach (var category in BuyMenuSelection.buyCategories)
        {
            foreach (var shopItem in category.items)
            {
                if (string.IsNullOrEmpty(shopItem.ammoId))
                    continue;

                var immutable = pContext.BuySelection[shopItem];
                if (immutable is not PistolItemClass pistolItem)
                    continue;

                if (shopItem.faction == pContext.Faction || shopItem.faction == Faction.None)
                    return pistolItem;
            }
        }

        return null;
    }

    public static SniperRifleItemClass GetFirstSniperRifleItem(PlayerContext pContext)
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

                if (shopItem.faction == pContext.Faction || shopItem.faction == Faction.None)
                    return assaultCarbine;
            }
        }

        return null;
    }

    public static void AddItem(ref List<Item> itemList, Item item)
    {
        if (itemList.Contains(item)) return;
        if (item == null) return;
        if (H.ShouldLog) D.LogInventory($"Adding {item.LocalizedName()} ({item.Id}) to removal list");

        itemList.Add(item);
    }

    public static void AddRange(ref List<Item> itemList, IEnumerable<Item> itemCollection)
    {
        foreach (Item item in itemCollection)
        {
            if (H.ShouldLog) D.LogInventory($"Adding {item.LocalizedName()} ({item.Id}) to removal list");
        }

        itemList.AddRange(itemCollection);
    }
}