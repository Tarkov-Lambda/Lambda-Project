using SearchableGrid = GClass3117;
using ItemExtensions = GClass3380;
using OperationResult = GStruct153;
//---------------------------------------------------------------//

using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using ifp.arena.bep.networking;
using UnityEngine;
using Cysharp.Threading.Tasks;

// Item flow summary:
//   ClientRequestGiveItem  – client checks it can make room, then sends SpawnItemPacket
//   SpawnItemPacketHandler – server approves, broadcasts to all clients, loads bundles
//   WhenApprovedGiveItem   – every client places the item in the correct slot/address
namespace ifp.arena.bep.Core
{
    // Describes how and where an item should land in a player's inventory.
    public enum PlacementKind { None, EquipmentSlot, VestAddress, ArmorPlate }

    public readonly struct ItemPlacement
    {
        public readonly PlacementKind Kind;
        public readonly EquipmentSlot Slot;        // valid when Kind == EquipmentSlot
        public readonly ItemAddress Address;        // valid when Kind == VestAddress
        public readonly CompoundItem PlateHolder;   // valid when Kind == ArmorPlate

        private ItemPlacement(PlacementKind kind, EquipmentSlot slot = default, ItemAddress address = null, CompoundItem plateHolder = null)
        {
            Kind = kind;
            Slot = slot;
            Address = address;
            PlateHolder = plateHolder;
        }

        public static ItemPlacement ForSlot(EquipmentSlot slot) => new(PlacementKind.EquipmentSlot, slot: slot);
        public static ItemPlacement ForAddress(ItemAddress address) => new(PlacementKind.VestAddress, address: address);
        public static ItemPlacement ForArmorPlate(CompoundItem holder) => new(PlacementKind.ArmorPlate, plateHolder: holder);
        public static readonly ItemPlacement None = new(PlacementKind.None);
    }

    public static class ItemsUtils
    {
        public static ItemFactoryClass ItemFactory => Singleton<ItemFactoryClass>.Instance;

        public static Item CreateItemFromTemplateId(string templateId) => ItemFactory.CreateItem(MongoID.Generate(), templateId, itemDiff: null);

        public static bool TryCreateItem(string templateId, out Item newItem)
        {
            newItem = null;
            if (!Singleton<ItemFactoryClass>.Instantiated || !Singleton<ItemFactoryClass>.Instance.ItemTemplates.ContainsKey(templateId))
                return false;
            newItem = ItemFactory.CreateItem(MongoID.Generate(), templateId, itemDiff: null);
            return newItem != null;
        }

        public static async UniTask<bool> ClientRequestGiveItem(Item templateItem)
        {
            if (templateItem == null)
                return false;

            var placement = GetItemPlacement(templateItem, H.MainPlayer);

            if (placement.Kind == PlacementKind.EquipmentSlot)
            {
                var slot = H.MainInventory.Equipment.GetSlot(placement.Slot);
                if (slot.ContainedItem is not null)
                {
                    bool removed;
                    if (templateItem is BackpackItemClass)
                        removed = await TryRemoveSlot(placement.Slot);
                    else
                    {
                        GStruct156<bool> result = H.MainInventoryController.TryThrowItem(slot.ContainedItem);
                        removed = result.Succeeded;
                    }

                    if (!removed)
                    {
                        H.Notify("Failed to allocate slot space in the inventory.");
                        return false;
                    }

                    await UniTask.Delay(300);
                }
            }

            await UniTask.Delay(300);
            Singleton<SpawnItemPacketHandler>.Instance.Send(ItemExtensions.CloneItem(templateItem));
            return true;
        }

        // THIS MUST ONLY BE CALLED WHEN THE PLAYER IS STANDING STILL
        // OTHERWISE THE INVENTORY CONTROLLER GETS LOCKED OUT FOREVER
        public static async UniTask<bool> TryRemoveSlot(EquipmentSlot equipmentSlot)
        {
            var slot = H.MainPlayer.Inventory.Equipment.GetSlot(equipmentSlot);
            if (slot.ContainedItem == null)
                return true;

            await UniTask.WaitUntil(() => !H.MainPlayer.MovementContext.CanWalk);
            await UniTask.Delay(200);

            return await TryRemoveItem(slot.ContainedItem, H.MainPlayer);
        }

        /// <summary>Removes any item from a player's inventory via a network transaction.</summary>
        public static async UniTask<bool> TryRemoveItem(Item item, Player player)
        {
            var removalEvent = InteractionsHandlerClass.Remove(item, player.InventoryController, true);
            if (removalEvent.Failed)
                return false;

            IResult result = await player.InventoryController.TryRunNetworkTransaction(removalEvent);
            return !result.Failed;
        }

        public static async void WhenApprovedGiveItem(Item item, Player player)
        {
            await PlaceItem(item, player, GetItemPlacement(item, player));

            if (item is Weapon weapon)
                SetupWeaponAfterEquip(weapon, player);

            if (player.IsYourPlayer)
                PlayEquipSound(item);
        }

        private static async UniTask PlaceItem(Item item, Player player, ItemPlacement placement)
        {
            switch (placement.Kind)
            {
                case PlacementKind.VestAddress:
                    player.InventoryController.AddAndRaiseEvents(item, placement.Address);
                    break;

                case PlacementKind.EquipmentSlot:
                    var slot = player.Equipment.GetSlot(placement.Slot);
                    slot.RemoveItemWithoutRestrictions();
                    player.InventoryController.AddAndRaiseEvents(item, slot.CreateItemAddress());
                    break;

                case PlacementKind.ArmorPlate:
                    await PlaceArmorPlate(item, player, placement.PlateHolder);
                    break;
            }
        }

        private static async UniTask PlaceArmorPlate(Item item, Player player, CompoundItem plateHolder)
        {
            foreach (ArmorHolderComponent armorHolder in plateHolder.Components.Where(c => c is ArmorHolderComponent))
            {
                foreach (var slot in armorHolder.ArmorSlots)
                {
                    if (slot.ContainedItem is not null)
                        continue;

                    if (slot.CachedSlotName is not ("Front_plate" or "Back_plate"))
                        continue;

                    var add = slot.AddWithoutRestrictions(item);
                    if (add.Succeeded)
                    {
                        await player.InventoryController.TryRunNetworkTransaction(add);
                        return;
                    }

                    H.Dump(add);
                }
            }
        }

        private static void SetupWeaponAfterEquip(Weapon weapon, Player player)
        {
            if (PresetUtils.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
            {
                PlayerUtils.ReplenishGun(weapon, ammo);
                PlayerUtils.ReplenishVestMagazines(weapon, ammo, player);
            }

            var firemode = weapon.Components.Find(c => c is FireModeComponent) as FireModeComponent;
            if (firemode != null && firemode.AvailableEFireModes.Contains(Weapon.EFireMode.fullauto))
                firemode.FireMode = Weapon.EFireMode.fullauto;
        }

        private static void PlayEquipSound(Item item)
        {
            AudioClip clip = Singleton<GUISounds>.Instance.GetItemClip(item.ItemSound, EInventorySoundType.drop);
            if (clip != null) Singleton<GUISounds>.Instance.PlaySound(clip);
        }

        public static ItemPlacement GetItemPlacement(Item item, Player player) => item switch
        {
            Weapon w => ResolveWeaponSlot(w),
            BackpackItemClass _ => ItemPlacement.ForSlot(EquipmentSlot.Backpack),
            HeadwearItemClass _ => ItemPlacement.ForSlot(EquipmentSlot.Headwear),
            ArmorPlateItemClass _ => ResolveArmorPlatePlacement(player),
            MagazineItemClass _ => ResolveVestAddress(item, player),
            MedicalItemClass _ => ResolveVestAddress(item, player),
            ThrowWeapItemClass _ => ResolveVestAddress(item, player),
            BarterItemItemClass _ => ResolveVestAddress(item, player),
            KeycardItemClass _ => ResolveVestAddress(item, player),
            _ => ItemPlacement.None
        };

        private static ItemPlacement ResolveWeaponSlot(Weapon weapon)
        {
            var slot = weapon is PistolItemClass ? EquipmentSlot.Holster : EquipmentSlot.FirstPrimaryWeapon;
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
            if (vest == null)
                return ItemPlacement.None;

            foreach (var container in vest.Containers)
            {
                if (container is SearchableGrid && container.TryFindLocationForItem(item, out ItemAddress location))
                    return ItemPlacement.ForAddress(location);
            }

            return ItemPlacement.None;
        }

        public static CompoundItem GetPlateHolder(Player player)
        {
            VestItemClass tacRig = player.Inventory.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as VestItemClass;
            if (tacRig != null && tacRig.Slots.Any())
                return tacRig;
            return null;
        }

        /// <summary>Returns all items currently occupying Front_plate / Back_plate slots in the player's rig.</summary>
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
                    if (slot.ContainedItem != null && slot.CachedSlotName is "Front_plate" or "Back_plate")
                        yield return slot.ContainedItem;
                }
            }
        }

        public static void SpawnAndEquip(Player player, string templateId, EquipmentSlot slotType)
        {
            if (TryCreateItem(templateId, out Item item))
            {
                var slot = player.Equipment.GetSlot(slotType);
                slot.RemoveItemWithoutRestrictions();
                slot.AddWithoutRestrictions(item);
            }
        }
    }
}
