using SearchableGrid = GClass3117;
using ItemExtensions = GClass3380;
using OperationResult = GStruct153;
//---------------------------------------------------------------//

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
using ifp.arena.bep.Core.MovementStates;

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
        public readonly EquipmentSlot Slot;         // valid when Kind == EquipmentSlot
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
        // Serializes concurrent ClientRequestGiveItem calls so the slot-check and the
        // resulting SpawnItemPacket.Send() are always atomic with respect to each other.
        // A fresh instance is created each session via ResetInventoryLock().
        private static SemaphoreSlim _giveItemLock = new SemaphoreSlim(1, 1);
        private static CancellationTokenSource _sessionCts = new CancellationTokenSource();

        // Call on game start AND game dispose so the lock and cancellation token are always fresh.
        public static void ResetInventoryLock()
        {
            _sessionCts.Cancel();
            _sessionCts.Dispose();
            _sessionCts = new CancellationTokenSource();
            // Replace rather than reset to handle the edge case where a caller is still
            // holding the semaphore when a raid ends.
            _giveItemLock = new SemaphoreSlim(1, 1);
        }

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

        public static async UniTask LoadBundlesForItem(Item item)
        {
            var prefabsToLoad = item.GetAllItems()
                .Select(i => i.Template.Prefab)
                .Where(p => p != null && !string.IsNullOrEmpty(p.path))
                .ToList();

            // Also include the ammo bundle for any weapons in the item tree.
            foreach (var subItem in item.GetAllItems())
            {
                if (subItem is Weapon weapon && PresetUtils.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
                {
                    var ammoPrefab = ammo.Template.Prefab;
                    if (ammoPrefab != null && !string.IsNullOrEmpty(ammoPrefab.path))
                        prefabsToLoad.Add(ammoPrefab);
                }
            }

            if (prefabsToLoad.Count > 0)
            {
                await Singleton<PoolManagerClass>.Instance.LoadBundlesAndCreatePools(
                    PoolManagerClass.PoolsCategory.Raid,
                    PoolManagerClass.AssemblyType.Local,
                    prefabsToLoad,
                    JobPriorityClass.Immediate,
                    null,
                    default(CancellationToken)
                );
            }
        }

        public static async UniTask<bool> ClientRequestGiveItem(Item templateItem)
        {
            if (templateItem == null)
                return false;

            // if another call is already in progress, wait for it to finish
            // before we check or mutate any slot state.
            try
            {
                await _giveItemLock.WaitAsync(_sessionCts.Token);
            }
            catch (OperationCanceledException)
            {
                return false; // Session ended while waiting — bail out cleanly
            }

            try
            {
                var placement = GetItemPlacement(templateItem, H.MainPlayer);

                if (placement.Kind == PlacementKind.EquipmentSlot)
                {
                    var slot = H.MainInventory.Equipment.GetSlot(placement.Slot);
                    if (slot.ContainedItem is not null)
                    {
                        bool removed;
                        if (templateItem is BackpackItemClass) // Backpack is only the bomb
                            removed = await TryRemoveSlot(placement.Slot, H.MainPlayer);
                        else
                        {
                            if (templateItem is Weapon)
                                removed = await TryThrowWeaponAndMags(placement.Slot, H.MainPlayer);
                            else
                                removed = await TryThrowSlot(placement.Slot, H.MainPlayer);
                        }

                        if (!removed)
                        {
                            H.Notify("Failed to allocate slot space in the inventory.");
                            return false;
                        }
                    }
                }

                await UniTask.Delay(100, cancellationToken: _sessionCts.Token);
                Singleton<SpawnItemPacketHandler>.Instance.Send(ItemExtensions.CloneItem(templateItem));
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                _giveItemLock.Release();
            }
        }

        // THIS MUST ONLY BE CALLED WHEN THE PLAYER IS STANDING STILL
        // OTHERWISE THE INVENTORY CONTROLLER GETS LOCKED OUT FOREVER
        public static async UniTask<bool> TryRemoveSlot(EquipmentSlot equipmentSlot, Player player, bool waitUntilStationary = true)
        {
            var slot = player.Inventory.Equipment.GetSlot(equipmentSlot);
            if (slot.ContainedItem == null) return true;

            if (waitUntilStationary)
            {
                await UniTask.WaitUntil(() => !player.MovementContext.CanWalk);
                await UniTask.Delay(200);
                if (equipmentSlot == EquipmentSlot.Backpack)
                {
                    await UniTask.WaitUntil(() =>
                    player.MovementContext.CurrentState is IdleStateClass ||
                    player.MovementContext.CurrentState is not SprintStateClass && player.MovementContext.Velocity.sqrMagnitude == 0f);
                }
            }

            return await TryRemoveItem(slot.ContainedItem, player);
        }

        /// <summary>Removes any item from a player's inventory via a network transaction.</summary>
        public static async UniTask<bool> TryRemoveItem(Item item, Player player)
        {
            OperationResult removalEvent = InteractionsHandlerClass.Remove(item, player.InventoryController, true);
            if (removalEvent.Failed) return false;

            IResult result = await player.InventoryController.TryRunNetworkTransaction(removalEvent);
            return !result.Failed;
        }

        public static async UniTask<bool> TryThrowSlot(EquipmentSlot equipmentSlot, Player player, bool waitUntilStationary = true)
        {
            var slot = player.Inventory.Equipment.GetSlot(equipmentSlot);
            if (slot.ContainedItem == null) return true;

            if (waitUntilStationary)
            {
                await UniTask.WaitUntil(() => !player.MovementContext.CanWalk);
                await UniTask.Delay(200);
            }

            return await TryThrowItem(slot.ContainedItem, player);
        }


        public static async UniTask<bool> TryThrowItem(Item item, Player player)
        {
            OperationResult removalEvent = InteractionsHandlerClass.Throw(item, player.InventoryController, true);
            if (removalEvent.Failed) return false;

            IResult result = await player.InventoryController.TryRunNetworkTransaction(removalEvent);
            return !result.Failed;
        }

        public static async UniTask<bool> TryThrowWeaponAndMags(EquipmentSlot equipmentSlot, Player player, bool waitUntilStationary = true)
        {
            var slot = player.Inventory.Equipment.GetSlot(equipmentSlot);
            if (slot.ContainedItem == null) return true;

            // Capture the mag template before the weapon is thrown.
            string oldMagTemplateId = slot.ContainedItem is Weapon oldWeapon ? oldWeapon.GetCurrentMagazine()?.TemplateId : null;
            bool removed = await TryThrowSlot(equipmentSlot, player, waitUntilStationary);

            if (removed && oldMagTemplateId != null)
            {
                var vest = player.Inventory.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as CompoundItem;

                if (vest != null)
                {
                    var magsToThrow = vest.Grids.SelectMany(g => g.Items)
                        .OfType<MagazineItemClass>()
                        .Where(m => m.TemplateId == oldMagTemplateId)
                        .ToList();

                    foreach (var mag in magsToThrow)
                        await TryThrowItem(mag, player);
                }
            }

            return removed;
        }

        public static async UniTask WhenApprovedGiveItem(Item item, Player player)
        {
            await PlaceItem(item, player, GetItemPlacement(item, player));
            // H.Notify($"Giving ${item.LocalizedName()} to {player.Profile.Nickname}");

            if (item is Weapon weapon) SetupWeaponAfterEquip(weapon, player);

            if (player.IsYourPlayer) PlayEquipSound(item);
        }

        private static async UniTask PlaceItem(Item item, Player player, ItemPlacement placement)
        {
            switch (placement.Kind)
            {
                case PlacementKind.VestAddress: // if we have an address, it means the space is free.
                    player.InventoryController.AddAndRaiseEvents(item, placement.Address);
                    break;

                case PlacementKind.EquipmentSlot:
                    var slot = player.Equipment.GetSlot(placement.Slot);
                    player.InventoryController.AddAndRaiseEvents(item, slot.CreateItemAddress());
                    break;

                case PlacementKind.ArmorPlate:
                    await PlaceArmorPlate(item, player, placement.PlateHolder);
                    break;
            }
        }

        private static async UniTask<bool> PlaceArmorPlate(Item item, Player player, CompoundItem plateHolder)
        {
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
                    if (addResult.Failed)
                    {
                        H.Dump(addResult);
                        return false;
                    }

                    // This is an extremely manual way of adding armor (and probably very fragile)
                    // however after spending an entire day throwing myself against the wall I must give up
                    // whilst this plate is registered correctly whilst the player is shot at
                    // the ui does not display any durability changes
                    // this is very likely due to me missing an action invocation somewhere that happens
                    // in the normal network transaction pipeline
                    var address = plate.CurrentAddress;
                    address.RaiseAddEvent(plate, CommandStatus.Begin, player.InventoryController);
                    address.RaiseAddEvent(plate, CommandStatus.Succeed, player.InventoryController);
                    slot.ApplyContainedItem();
                    // player.OnArmorPointsChanged(plate.Armor, true);

                    return true;
                }
            }
            return false;
        }

        private static void SetupWeaponAfterEquip(Weapon weapon, Player player)
        {
            if (PresetUtils.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
            {
                PlayerUtils.ReplenishGun(weapon, ammo);

                // Only the local player's machine should create and broadcast vest magazines.
                if (player.IsYourPlayer)
                    PlayerUtils.ReplenishVestMagazines(weapon, ammo, player);
            }

            var firemode = weapon.Components.Find(c => c is FireModeComponent) as FireModeComponent;
            if (firemode != null && firemode.AvailableEFireModes.Contains(Weapon.EFireMode.fullauto))
            {
                firemode.FireMode = Weapon.EFireMode.fullauto;
            }
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
            if (vest == null) return ItemPlacement.None;

            bool isOneByOne = item.Template.Width == 1 && item.Template.Height == 1;

            // For 1x1 items, prefer placing them in a 1x1 grid first.
            if (isOneByOne)
            {
                foreach (var container in vest.Containers)
                {
                    if (container is SearchableGrid grid && grid.GridWidth == 1 && grid.GridHeight == 1
                        && container.TryFindLocationForItem(item, out ItemAddress location))
                    {
                        return ItemPlacement.ForAddress(location);
                    }
                }
            }

            // Default, try any grid.
            foreach (var container in vest.Containers)
            {
                if (container is SearchableGrid && container.TryFindLocationForItem(item, out ItemAddress location))
                {
                    return ItemPlacement.ForAddress(location);
                }
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
                    if (slot.ContainedItem != null && slot.CachedSlotName.EndsWith("_plate", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return slot.ContainedItem;
                    }
                }
            }
        }
    }
}
