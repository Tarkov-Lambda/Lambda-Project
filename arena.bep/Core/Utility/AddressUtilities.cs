using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using ifp.arena.bep.networking;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using EFT.Interactive;
using Fika.Core.Main.FreeCamera.Patches;

namespace ifp.arena.bep.Core
{
    // Where to place the item (none = tough luck)
    public enum PlacementKind { None, EquipmentSlot, VestAddress, ArmorPlate }

    public readonly struct ItemPlacement
    {
        public readonly PlacementKind Kind;
        public readonly EquipmentSlot Slot;         // For EquipmentSlot
        public readonly ItemAddress Address;        // For VestAddress

        public ItemPlacement(PlacementKind kind, EquipmentSlot slot = default, ItemAddress address = null, CompoundItem plateHolder = null)
        {
            Kind = kind;
            Slot = slot;
            Address = address;
        }

        public static ItemPlacement ForSlot(EquipmentSlot slot) => new(PlacementKind.EquipmentSlot, slot: slot);
        public static ItemPlacement ForAddress(ItemAddress address) => new(PlacementKind.VestAddress, address: address);
        public static ItemPlacement ForArmorPlate(CompoundItem holder) => new(PlacementKind.ArmorPlate, plateHolder: holder);
        public static readonly ItemPlacement None = new(PlacementKind.None);
    }

    // 1. ClientRequestGiveItem client checks it can make room, then sends SpawnItemPacket
    // 2. SpawnItemPacketHandler server approves, broadcasts to all clients, loads bundles, executes WhenApprovedGiveItem
    // 3. WhenApprovedGiveItem every client places the item in the correct slot/address (for each player on the server)
    public static class AddressUtilities
    {
        private static SemaphoreSlim _giveItemLock = new SemaphoreSlim(1, 1);
        private static CancellationTokenSource _sessionCts = new CancellationTokenSource();

       

        private static async UniTask PlaceItem(Item item, Player player, ItemPlacement placement)
        {
            switch (placement.Kind)
            {
                case PlacementKind.VestAddress: // if we have an address, it means the space is free.
                    D.LogTransaction($"Placing item {item.LocalizedName()} ({item.Id}) in {player.Profile.Nickname} inventory at {placement.Address}");
                    player.InventoryController.AddAndRaiseEvents(item, placement.Address);
                    break;

                case PlacementKind.EquipmentSlot:
                    D.LogTransaction($"Placing item {item.LocalizedName()} ({item.Id}) in {player.Profile.Nickname} inventory at {placement.Address}");
                    var slot = player.Equipment.GetSlot(placement.Slot);
                    player.InventoryController.AddAndRaiseEvents(item, slot.CreateItemAddress());
                    break;

                case PlacementKind.ArmorPlate:
                    await PlaceArmorPlate(item, player, placement);
                    break;
            }
        }

        private static async UniTask<bool> PlaceArmorPlate(Item item, Player player, ItemPlacement placement)
        {
            // var plateHolder = PU.GetPlayerSlotItem(player, placement.Slot) as CompoundItem;
            var plateHolder = GetPlateHolder(player);

            var plate = item as ArmorPlateItemClass;
            foreach (ArmorHolderComponent armorHolder in plateHolder.Components.Where(c => c is ArmorHolderComponent))
            {
                foreach (var slot in armorHolder.ArmorSlots)
                {
                    if (slot.ContainedItem is not null)
                        continue;
                    if (slot.CachedSlotName != null && !slot.CachedSlotName.EndsWith("_plate", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var addResult = slot.AddWithoutRestrictions(plate);
                    D.Log(slot.CreateItemAddress().ContainerName);
                    D.Log(plate.CurrentAddress.ContainerName);

                    if (addResult.Failed)
                    {
                        // D.Dump(addResult);
                        return false;
                    }

                    // This is an extremely manual way of adding armor
                    // however after spending an entire day throwing myself against the wall I must give up
                    // whilst this plate is registered correctly when the player is shot at
                    // the ui does not display any durability changes
                    // this is very likely due to me missing a listener somewhere that happens
                    // in the normal network transaction pipeline
                    // Sidenote: I could lowkey patch out Slot.Add() specifically for plates to bypass "locked slot" error
                    plate.CurrentAddress.RaiseAddEvent(plate, CommandStatus.Begin, player.InventoryController);
                    plate.CurrentAddress.RaiseAddEvent(plate, CommandStatus.Succeed, player.InventoryController);
                    slot.ApplyContainedItem();

                    return true;
                }
            }
            return false;
        }

        private static void PlayEquipSound(Item item)
        {
            AudioClip clip = H.GUISounds.GetItemClip(item.ItemSound, EInventorySoundType.drop);
            if (clip != null) H.GUISounds.PlaySound(clip);
        }

        public static ItemPlacement GetItemPlacement(Item item, Player player) => item switch
        {
            Weapon w => ResolveWeaponSlot(w),

            BackpackItemClass _ => ItemPlacement.ForSlot(EquipmentSlot.Backpack),
            VestItemClass _ => ItemPlacement.ForSlot(EquipmentSlot.TacticalVest),
            ArmorItemClass _ => ItemPlacement.ForSlot(EquipmentSlot.ArmorVest),
            HeadwearItemClass _ => ItemPlacement.ForSlot(EquipmentSlot.Headwear),
            FaceCoverItemClass _ => ItemPlacement.ForSlot(EquipmentSlot.FaceCover),
            HeadphonesItemClass _ => ItemPlacement.ForSlot(EquipmentSlot.Earpiece),

            ArmorPlateItemClass _ => ResolveArmorPlatePlacement(player),

            MagazineItemClass _ => ResolveVestAddress(item, player),
            MedicalItemClass _ => ResolveVestAddress(item, player),
            ThrowWeapItemClass _ => ResolveVestAddress(item, player),
            BarterItemItemClass _ => ResolveVestAddress(item, player),
            KeycardItemClass _ => ResolveVestAddress(item, player), // in case we're on labs and the bomb site is in red room type beat

            _ => ItemPlacement.None
        };

        // revolver shotgun is fucked gg
        private static ItemPlacement ResolveWeaponSlot(Weapon weapon)
        {
            var slot = weapon is PistolItemClass or RevolverItemClass ? EquipmentSlot.Holster : EquipmentSlot.FirstPrimaryWeapon;
            return ItemPlacement.ForSlot(slot);
        }

        private static ItemPlacement ResolveArmorPlatePlacement(Player player)
        {
            var plateHolder = GetPlateHolder(player);
            return plateHolder != null ? ItemPlacement.ForArmorPlate(plateHolder) : ItemPlacement.None;
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
