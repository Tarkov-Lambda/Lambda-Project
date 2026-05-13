using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
using System;
using EFT.Interactive;

namespace Lambda.Core.Main;

// Where to place the item (none = tough luck)
public enum PlacementKind { None, EquipmentSlot, VestAddress, ArmorPlate }

public readonly struct ItemPlacement(PlacementKind kind, EquipmentSlot slot = default, ItemAddress address = null)
{
    public readonly PlacementKind Kind = kind;
    public readonly EquipmentSlot Slot = slot; // for EquipmentSlot
    public readonly ItemAddress Address = address;

    public static ItemPlacement ForSlot(EquipmentSlot slot, ItemAddress address) => new(PlacementKind.EquipmentSlot, slot: slot, address: address);
    public static ItemPlacement ForAddress(ItemAddress address) => new(PlacementKind.VestAddress, address: address);
    public static ItemPlacement ForArmorPlate(ItemAddress address) => new(PlacementKind.ArmorPlate, address: address);
    public static readonly ItemPlacement None = new(PlacementKind.None);
}

// literally my way on the highway type beat way of determining where shit goes
public static class AddressUtilities
{
    public static ItemPlacement GetItemPlacement(Item item, Player player) => item switch
    {
        Weapon w => ResolveWeaponSlot(w, player),

        BackpackItemClass _ => ResolveSlotAddress(EquipmentSlot.Backpack, player),
        VestItemClass _ => ResolveSlotAddress(EquipmentSlot.TacticalVest, player),
        ArmorItemClass _ => ResolveSlotAddress(EquipmentSlot.ArmorVest, player),
        HeadwearItemClass _ => ResolveSlotAddress(EquipmentSlot.Headwear, player),
        FaceCoverItemClass _ => ResolveSlotAddress(EquipmentSlot.FaceCover, player),
        HeadphonesItemClass _ => ResolveSlotAddress(EquipmentSlot.Earpiece, player),
        VisorsItemClass _ => ResolveSlotAddress(EquipmentSlot.Eyewear, player),
        KnifeItemClass _ => ResolveSlotAddress(EquipmentSlot.Scabbard, player),

        ArmorPlateItemClass _ => ResolveArmorPlatePlacement(player),

        MagazineItemClass _ => ResolveVestAddress(item, player),
        MedicalItemClass _ => ResolveVestAddress(item, player),
        ThrowWeapItemClass _ => ResolveVestAddress(item, player),
        BarterItemItemClass _ => ResolveVestAddress(item, player),
        KeycardItemClass _ => ResolveVestAddress(item, player), // in case we're on labs and the bomb site is in red room type beat

        _ => ResolveVestAddress(item, player)
    };

    // revolver shotgun is fucked gg
    private static ItemPlacement ResolveWeaponSlot(Weapon weapon, Player player)
    {
        var slot = weapon is PistolItemClass or RevolverItemClass ? EquipmentSlot.Holster : EquipmentSlot.FirstPrimaryWeapon;
        return ResolveSlotAddress(slot, player);
    }

    private static ItemPlacement ResolveArmorPlatePlacement(Player player)
    {
        var plateHolder = player.GetPlateCarrier();
        if (plateHolder == null) return ItemPlacement.None;

        foreach (ArmorHolderComponent armorHolder in plateHolder.Components.Where(c => c is ArmorHolderComponent))
        {
            foreach (var slot in armorHolder.ArmorSlots)
            {
                if (slot.ContainedItem != null)
                    continue;

                bool isPlateSlot = (!string.IsNullOrEmpty(slot.Name) && slot.Name.EndsWith("_plate", StringComparison.OrdinalIgnoreCase)) ||
                                   (slot.CachedSlotName != null && slot.CachedSlotName.EndsWith("_plate", StringComparison.OrdinalIgnoreCase));

                if (!isPlateSlot)
                    continue;

                return ItemPlacement.ForArmorPlate(slot.CreateItemAddress());
            }
        }

        foreach (var slot in plateHolder.Slots)
        {
            if (slot.ContainedItem != null) continue;

            if (!string.IsNullOrEmpty(slot.Name) && slot.Name.EndsWith("_plate", StringComparison.OrdinalIgnoreCase))
            {
                return ItemPlacement.ForArmorPlate(slot.CreateItemAddress());
            }
        }

        return ItemPlacement.None;
    }

    private static ItemPlacement ResolveSlotAddress(EquipmentSlot slotType, Player player)
    {
        return ItemPlacement.ForSlot(slotType, player.Inventory.Equipment.GetSlot(slotType).CreateItemAddress());
    }

    private static ItemPlacement ResolveVestAddress(Item item, Player player)
    {
        var vest = player.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as SearchableItemItemClass;
        if (vest == null) return ItemPlacement.None;

        var pockets = player.GetPlayerPockets();
        var allContainers = pockets.Containers.Concat(vest.Containers);

        bool isOneByOne = item.Template.Width == 1 && item.Template.Height == 1;

        ItemPlacement FindPlacement(IEnumerable<EFT.InventoryLogic.IContainer> containers, Func<SearchableGrid, bool> gridFilter)
        {
            foreach (var container in containers.OfType<SearchableGrid>().Where(gridFilter))
            {
                if (container.TryFindLocationForItem(item, out ItemAddress location))
                    return ItemPlacement.ForAddress(location);
            }
            return ItemPlacement.None;
        }

        // prefer 1x1 grids for 1x1 items
        if (isOneByOne)
        {
            var placement = FindPlacement(allContainers, g => g.GridWidth == 1 && g.GridHeight == 1);
            if (!placement.Equals(ItemPlacement.None))
                return placement;
        }

        // fallback to any grid
        return FindPlacement(allContainers, _ => true);
    }
}