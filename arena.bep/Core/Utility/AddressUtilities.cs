using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
using System;
using EFT.Interactive;

namespace ifp.arena.bep.Core
{
    // Where to place the item (none = tough luck)
    public enum PlacementKind { None, EquipmentSlot, VestAddress, ArmorPlate }

    public readonly struct ItemPlacement(PlacementKind kind, EquipmentSlot slot = default, ItemAddress address = null)
    {
        public readonly PlacementKind Kind = kind;
        public readonly EquipmentSlot Slot = slot; // for EquipmentSlot
        public readonly ItemAddress Address = address;

        public static ItemPlacement ForSlot(EquipmentSlot slot, ItemAddress address) => new(PlacementKind.EquipmentSlot, slot: slot, address: address);
        public static ItemPlacement ForAddress(ItemAddress address) => new(PlacementKind.VestAddress, address: address);
        public static ItemPlacement ForArmorPlate() => new(PlacementKind.ArmorPlate); // I really should pass address into this
        public static readonly ItemPlacement None = new(PlacementKind.None);
    }

    // 1. ClientRequestGiveItem client checks it can make room, then sends SpawnItemPacket
    // 2. SpawnItemPacketHandler server approves, broadcasts to all clients, loads bundles, executes WhenApprovedGiveItem
    // 3. WhenApprovedGiveItem every client places the item in the correct slot/address (for each player on the server)
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

            ArmorPlateItemClass _ => ResolveArmorPlatePlacement(player),

            MagazineItemClass _ => ResolveVestAddress(item, player),
            MedicalItemClass _ => ResolveVestAddress(item, player),
            ThrowWeapItemClass _ => ResolveVestAddress(item, player),
            BarterItemItemClass _ => ResolveVestAddress(item, player),
            KeycardItemClass _ => ResolveVestAddress(item, player), // in case we're on labs and the bomb site is in red room type beat

            _ => ItemPlacement.None
        };

        // revolver shotgun is fucked gg
        private static ItemPlacement ResolveWeaponSlot(Weapon weapon, Player player)
        {
            var slot = weapon is PistolItemClass or RevolverItemClass ? EquipmentSlot.Holster : EquipmentSlot.FirstPrimaryWeapon;
            return ResolveSlotAddress(slot, player);
        }

        private static ItemPlacement ResolveArmorPlatePlacement(Player player)
        {
            return GetPlateHolder(player) != null ? ItemPlacement.ForArmorPlate() : ItemPlacement.None;
        }

        private static ItemPlacement ResolveSlotAddress(EquipmentSlot slotType, Player player)
        {
            return ItemPlacement.ForSlot(slotType, player.Inventory.Equipment.GetSlot(slotType).CreateItemAddress());
        }

        private static ItemPlacement ResolveVestAddress(Item item, Player player)
        {
            var vest = player.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as SearchableItemItemClass;
            if (vest == null) return ItemPlacement.None;

            var pockets = PU.GetPlayerPockets(player);
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

            // Prefer 1x1 grids for 1x1 items
            if (isOneByOne)
            {
                var placement = FindPlacement(allContainers, g => g.GridWidth == 1 && g.GridHeight == 1);
                if (!placement.Equals(ItemPlacement.None))
                    return placement;
            }

            // Fallback to any grid
            return FindPlacement(allContainers, _ => true);
        }

        public static CompoundItem GetPlateHolder(Player player)
        {
            VestItemClass tacRig = player.Inventory.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as VestItemClass;
            if (tacRig != null)
            {
                if (IsTacRigArmored(tacRig))
                {
                    return tacRig;
                }
            }

            ArmorItemClass armorVest = player.Inventory.Equipment.GetSlot(EquipmentSlot.ArmorVest).ContainedItem as ArmorItemClass;
            if (armorVest != null)
                return armorVest;

            return null;
        }

        public static bool IsTacRigArmored(VestItemClass tacRig)
        {
            var tacRigTemplate = tacRig?.Template as VestTemplateClass;
            if (tacRigTemplate.BlocksArmorVest) return true;
            return false;
        }

        public static IEnumerable<Item> GetArmorPlates(Player player)
        {
            var plateHolder = GetPlateHolder(player);
            if (plateHolder == null)
                yield break;

            foreach (var component in plateHolder.Components)
            {
                if (component is not ArmorHolderComponent armorHolder)
                    continue;

                foreach (var slot in armorHolder.ArmorSlots)
                {
                    if (slot.ContainedItem != null && slot.CachedSlotName != null && slot.CachedSlotName.EndsWith("_plate", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return slot.ContainedItem;
                    }
                }
            }
        }

        public static void GarbageCollectWorldLoot()
        {
            ObservedLootItem[] allLoot = GameObject.FindObjectsByType<ObservedLootItem>(FindObjectsSortMode.None);

            foreach (ObservedLootItem loot in allLoot)
            {
                if (!loot.isActiveAndEnabled) continue;
                loot.Kill();
            }
        }
    }
}
