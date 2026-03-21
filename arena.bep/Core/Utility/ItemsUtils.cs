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
using EFT.Interactive;

namespace ifp.arena.bep.Core
{
    // Where to place the item (none = tough luck)
    public enum PlacementKind { None, EquipmentSlot, VestAddress, ArmorPlate }

    public readonly struct ItemPlacement
    {
        public readonly PlacementKind Kind;
        public readonly EquipmentSlot Slot;         // For EquipmentSlot
        public readonly ItemAddress Address;        // For VestAddress
        public readonly CompoundItem PlateHolder;   // For ArmorPlate

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

    // 1. ClientRequestGiveItem client checks it can make room, then sends SpawnItemPacket
    // 2. SpawnItemPacketHandler server approves, broadcasts to all clients, loads bundles, executes WhenApprovedGiveItem
    // 3. WhenApprovedGiveItem every client places the item in the correct slot/address (for each player on the server)
    public static class ItemsUtils
    {
        private static SemaphoreSlim _giveItemLock = new SemaphoreSlim(1, 1);
        private static CancellationTokenSource _sessionCts = new CancellationTokenSource();

        // OnGameStarted / OnGameDispose
        public static void ResetInventoryLock()
        {
            _sessionCts.Cancel();
            _sessionCts.Dispose();
            _sessionCts = new CancellationTokenSource();
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
                if (subItem is Weapon weapon && FactoryUtils.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
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
                return false; // Session ended
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
                Item clonedItem = ItemExtensions.CloneItem(templateItem);
                H.LogTransaction($"Player {H.MainPlayer.Profile.Nickname} is requesting {clonedItem.LocalizedName()} ({clonedItem.Id})");
                Singleton<SpawnItemPacketHandler>.Instance.Send(clonedItem);
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
            => await TryOperateOnSlot(equipmentSlot, player, TryRemoveItem, waitUntilStationary, extraBackpackWait: true);

        public static async UniTask<bool> TryThrowSlot(EquipmentSlot equipmentSlot, Player player, bool waitUntilStationary = true)
            => await TryOperateOnSlot(equipmentSlot, player, TryThrowItem, waitUntilStationary);


        private static async UniTask<bool> TryOperateOnSlot(
            EquipmentSlot equipmentSlot,
            Player player,
            Func<Item, Player, UniTask<bool>> operation,
            bool waitUntilStationary,
            bool extraBackpackWait = false)
        {
            Item item = PlayerUtils.GetPlayerSlotItem(player, equipmentSlot);
            if (item == null) return true;

            if (waitUntilStationary)
            {
                await PlayerUtils.WaitUntilStationary(player);
                if (extraBackpackWait && equipmentSlot == EquipmentSlot.Backpack)
                {
                    await UniTask.WaitUntil(() =>
                        player.MovementContext.CurrentState is IdleStateClass ||
                        player.MovementContext.CurrentState is not SprintStateClass && player.MovementContext.Velocity.sqrMagnitude == 0f);
                }
            }

            return await operation(item, player);
        }


        public static async UniTask<bool> TryRemoveItem(Item item, Player player)
        {
            OperationResult removalEvent = InteractionsHandlerClass.Remove(item, player.InventoryController, true);
            if (removalEvent.Failed) return false;

            IResult result = await player.InventoryController.TryRunNetworkTransaction(removalEvent);
            return !result.Failed;
        }

        public static async UniTask TryRemoveItems(IEnumerable<Item> items, Player player, int delayMs = 25)
        {
            foreach (var item in items)
            {
                await TryRemoveItem(item, player);
                await UniTask.Delay(delayMs);
            }
        }


        public static async UniTask<bool> TryThrowItem(Item item, Player player)
        {
            H.LogTransaction($"Player {player.Profile.Nickname} is trying to create throw event for {item.LocalizedName()} ({item.Id})");
            OperationResult removalEvent = InteractionsHandlerClass.Throw(item, player.InventoryController, true);
            if (removalEvent.Failed)
            {
                H.LogTransaction($"Player {player.Profile.Nickname} failed to execute throw simulation for {item.LocalizedName()} ({item.Id})");
                H.LogTransaction($"Reason: {removalEvent.Error}");
            }
            if (removalEvent.Failed) return false;

            IResult result = await player.InventoryController.TryRunNetworkTransaction(removalEvent);
            if (result.Failed)
            {
                H.LogTransaction($"Player {player.Profile.Nickname} got an error for throwing network transaction event for {item.LocalizedName()} ({item.Id})");
                H.LogTransaction($"Reason: {result.Error}");
            }
            return !result.Failed;
        }

        public static async UniTask<bool> TryThrowWeaponAndMags(EquipmentSlot equipmentSlot, Player player, bool waitUntilStationary = true)
        {
            var slot = player.Inventory.Equipment.GetSlot(equipmentSlot);
            if (slot.ContainedItem == null) return true;

            List<MagazineItemClass> magsToThrow = null;
            if (slot.ContainedItem is Weapon oldWeapon)
            {
                string oldMagTemplateId = oldWeapon.GetCurrentMagazine()?.TemplateId;
                if (oldMagTemplateId != null)
                {
                    var vest = PlayerUtils.GetPlayerSlotItem(player, EquipmentSlot.TacticalVest) as CompoundItem;
                    magsToThrow = PlayerUtils.GetMatchingMags(player, vest, oldMagTemplateId);
                    H.Log(magsToThrow.Count.ToString());
                }
            }

            bool removed = await TryThrowSlot(equipmentSlot, player, waitUntilStationary);

            if (removed && magsToThrow != null)
            {
                foreach (var mag in magsToThrow)
                {
                    H.LogTransaction($"Throwing away {mag.LocalizedName()} ({mag.Id})");
                    await TryThrowItem(mag, player);
                }
            }

            return removed;
        }

        public static async UniTask WhenApprovedGiveItem(Item item, Player player)
        {
            await PlaceItem(item, player, GetItemPlacement(item, player));
            // H.Notify($"Giving ${item.LocalizedName()} to {player.Profile.Nickname}");

            if (item is Weapon weapon) ReplenishUtils.SetupWeaponAfterEquip(weapon, player);

            if (player.IsYourPlayer) PlayEquipSound(item);
        }

        private static async UniTask PlaceItem(Item item, Player player, ItemPlacement placement)
        {
            switch (placement.Kind)
            {
                case PlacementKind.VestAddress: // if we have an address, it means the space is free.
                    H.LogTransaction($"Placing item {item.LocalizedName()} ({item.Id}) in {player.Profile.Nickname} inventory at {placement.Address}");
                    player.InventoryController.AddAndRaiseEvents(item, placement.Address);
                    break;

                case PlacementKind.EquipmentSlot:
                    H.LogTransaction($"Placing item {item.LocalizedName()} ({item.Id}) in {player.Profile.Nickname} inventory at {placement.Address}");
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
            AudioClip clip = Singleton<GUISounds>.Instance.GetItemClip(item.ItemSound, EInventorySoundType.drop);
            if (clip != null) Singleton<GUISounds>.Instance.PlaySound(clip);
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

            var pockets = PlayerUtils.GetPlayerPockets(player);
            bool isOneByOne = item.Template.Width == 1 && item.Template.Height == 1;

            // For 1x1 items, prefer placing them in a 1x1 grid first.
            if (isOneByOne)
            {
                foreach (var container in pockets.Containers)
                {
                    if (container is SearchableGrid grid && grid.GridWidth == 1 && grid.GridHeight == 1 && container.TryFindLocationForItem(item, out ItemAddress location))
                    {
                        return ItemPlacement.ForAddress(location);
                    }
                }
                foreach (var container in vest.Containers)
                {
                    if (container is SearchableGrid grid && grid.GridWidth == 1 && grid.GridHeight == 1 && container.TryFindLocationForItem(item, out ItemAddress location))
                    {
                        return ItemPlacement.ForAddress(location);
                    }
                }
            }

            // Default, try any grid.
            foreach (var container in pockets.Containers)
            {
                if (container is SearchableGrid && container.TryFindLocationForItem(item, out ItemAddress location))
                {
                    return ItemPlacement.ForAddress(location);
                }
            }

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
            if (tacRigTemplate != null && tacRigTemplate.BlocksArmorVest) return true;
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
                    if (slot.ContainedItem != null && slot.CachedSlotName.EndsWith("_plate", StringComparison.OrdinalIgnoreCase))
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
                if (!loot.isActiveAndEnabled)
                    continue;
                loot.Kill();
            }
        }
    }
}
