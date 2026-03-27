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

namespace ifp.arena.bep.Core
{
    // 1. ClientRequestGiveItem client checks it can make room, then sends SpawnItemPacket
    // 2. SpawnItemPacketHandler server approves, broadcasts to all clients, loads bundles, executes WhenApprovedGiveItem
    // 3. WhenApprovedGiveItem every client places the item in the correct slot/address (for each player on the server)
    public static class ItemUtilities
    {
        // private static SemaphoreSlim _giveItemLock = new SemaphoreSlim(1, 1);
        // private static CancellationTokenSource _sessionCts = new CancellationTokenSource();

        // // OnGameStarted / OnGameDispose
        // public static void ResetInventoryLock()
        // {
        //     _sessionCts.Cancel();
        //     _sessionCts.Dispose();
        //     _sessionCts = new CancellationTokenSource();
        //     _giveItemLock = new SemaphoreSlim(1, 1);
        // }

        public static Item CreateItemFromTemplateId(string templateId) => FU.ItemFactory.CreateItem(MongoID.Generate(), templateId, itemDiff: null);

        public static bool TryCreateItem(string templateId, out Item newItem)
        {
            newItem = null;
            if (!FU.ItemFactory.ItemTemplates.ContainsKey(templateId))
                return false;
            newItem = FU.ItemFactory.CreateItem(MongoID.Generate(), templateId, itemDiff: null);
            return newItem != null;
        }

        public static async UniTask LoadBundlesForItem(Item item)
        {
            var prefabsToLoad = item.GetAllItems()
                .Select(i => i.Template.Prefab)
                .Where(p => p != null && !string.IsNullOrEmpty(p.path))
                .ToList();

            // also include the ammo bundle for any weapons in the item tree
            foreach (var subItem in item.GetAllItems())
            {
                if (subItem is Weapon weapon && FU.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
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
                // try
                // {
                //     await _giveItemLock.WaitAsync(_sessionCts.Token);
                // }
                // catch (OperationCanceledException)
                // {
                //     return false; // Session ended
                // }

                // try
                // {
                var placement = AU.GetItemPlacement(templateItem, H.MainPlayer);

                if (placement.Kind == PlacementKind.EquipmentSlot)
                {
                    var slot = H.MainInventory.Equipment.GetSlot(placement.Slot);
                    if (slot.ContainedItem is not null)
                    {
                        bool removed;
                        if (templateItem is BackpackItemClass) // Backpack is only the bomb
                            removed = await TryPopContainedItem(placement.Slot, H.MainPlayer);
                        else
                        {
                            if (templateItem is Weapon)
                                removed = await TryThrowWeaponAndMags(placement.Slot, H.MainPlayer);
                            else
                                removed = await TryThrowContainedItem(placement.Slot, H.MainPlayer);
                        }

                        if (!removed)
                        {
                            D.Notify("Failed to allocate slot space in the inventory.");
                            return false;
                        }
                    }
                }

                // await UniTask.Delay(100, cancellationToken: _sessionCts.Token);
                Item clonedItem = templateItem.CloneItem(H.MainPlayer.InventoryController);
                clonedItem.StackObjectsCount = 1;
                // D.LogTransaction($"{H.MainPlayer.Profile.Nickname} requesting {clonedItem.LocalizedShortName()} ({clonedItem.Id}) at ({placement.Address})");
                Singleton<SpawnItemPacketHandler>.Instance.Send(clonedItem, placement);
                return true;
            // }
            // catch (OperationCanceledException)
            // {
            //     return false;
            // }
            // finally
            // {
            //     _giveItemLock.Release();
            // }
        }


        public static async UniTask WhenApprovedGiveItem(Item item, Player player, ItemPlacement placement)
        {
            // var localPlacement = AU.GetItemPlacement(item, player);
            await PlaceItem(item, player, placement);


            if (item is Weapon weapon) RU.SetupWeaponAfterEquip(weapon, player);
            if (player.IsYourPlayer) PlayEquipSound(item);
        }

        // THIS MUST ONLY BE CALLED WHEN THE PLAYER IS STANDING STILL
        // OTHERWISE THE INVENTORY CONTROLLER GETS LOCKED OUT FOREVER
        public static async UniTask<bool> TryPopContainedItem(EquipmentSlot equipmentSlot, Player player, bool waitUntilStationary = true)
            => await TryOperateOnSlot(equipmentSlot, player, TryPopItem, waitUntilStationary, extraBackpackWait: true);

        public static async UniTask<bool> TryThrowContainedItem(EquipmentSlot equipmentSlot, Player player, bool waitUntilStationary = true)
            => await TryOperateOnSlot(equipmentSlot, player, TryThrowItem, waitUntilStationary);


        private static async UniTask<bool> TryOperateOnSlot(
            EquipmentSlot equipmentSlot,
            Player player,
            Func<Item, Player, UniTask<bool>> operation,
            bool waitUntilStationary,
            bool extraBackpackWait = false)
        {
            Item item = PU.GetPlayerSlotItem(player, equipmentSlot);
            if (item == null) return true;

            if (waitUntilStationary)
            {
                await PU.WaitUntilStationary(player);
                if (extraBackpackWait && equipmentSlot == EquipmentSlot.Backpack)
                {
                    await UniTask.WaitUntil(() =>
                        player.MovementContext.CurrentState is IdleStateClass ||
                        player.MovementContext.CurrentState is not SprintStateClass && player.MovementContext.Velocity.sqrMagnitude == 0f);
                }
            }

            return await operation(item, player);
        }



        public static async UniTask TryPopItems(IEnumerable<Item> items, Player player, int delayMs = 25)
        {
            foreach (var item in items)
            {
                await TryPopItem(item, player);
                if (delayMs != 0) await UniTask.Delay(delayMs);
            }
        }

        public static async UniTask<bool> TryPopItem(Item item, Player player)
        {
            var address = item.CurrentAddress;
            bool result = await TryDoItemAction(item, player, InteractionsHandlerClass.Remove, "remove");
            if (result && item is ArmorPlateItemClass)
            {
                Singleton<RefreshPlateAddressPacketHandler>.Instance.Send(address);
            }

            return result;
        }

        public static async UniTask TryThrowItems(IEnumerable<Item> items, Player player, int delayMs = 25)
        {
            foreach (var item in items)
            {
                await TryThrowItem(item, player);
                if (delayMs != 0) await UniTask.Delay(delayMs);
            }
        }

        public static UniTask<bool> TryThrowItem(Item item, Player player)
        {
            return TryDoItemAction(item, player, InteractionsHandlerClass.Throw, "throw");
        }

        public static async UniTask<bool> TryDoItemAction<T>(
            Item item,
            Player player,
            Func<Item, InventoryController, bool, GStruct154<T>> action, string actionName) where T : IRaiseEvents
        {
            D.LogInventory($"Player {player.Profile.Nickname} is trying to {actionName} {item.LocalizedName()} ({item.Id})");

            var opResult = action(item, player.InventoryController, true);

            if (opResult.Failed)
            {
                D.LogTransaction($"Player {player.Profile.Nickname} failed to execute {actionName} simulation for {item.LocalizedName()} ({item.Id})");
                D.LogTransaction($"Reason: {opResult.Error}");
                return false;
            }

            IResult result = await player.InventoryController.TryRunNetworkTransaction(opResult);

            if (result.Failed)
            {
                D.LogTransaction($"Player {player.Profile.Nickname} got an error for {actionName} network transaction for {item.LocalizedName()} ({item.Id})");
                D.LogTransaction($"Reason: {result.Error}");
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
                    var vest = PU.GetPlayerSlotItem(player, EquipmentSlot.TacticalVest) as CompoundItem;
                    magsToThrow = PU.GetMatchingMags(player, vest, oldMagTemplateId);
                }
            }

            bool removed = await TryThrowContainedItem(equipmentSlot, player, waitUntilStationary);

            if (removed && magsToThrow != null)
            {
                await TryThrowItems(magsToThrow, player, 25);
            }

            return removed;
        }


        private static async UniTask PlaceItem(Item item, Player player, ItemPlacement placement)
        {
            switch (placement.Kind)
            {
                case PlacementKind.VestAddress: // if we have an address, it means the space is free.
                    // D.LogTransaction($"Placing item {item.LocalizedName()} ({item.Id}) in {player.Profile.Nickname} inventory at {placement.Address}");
                    D.Dump(placement.Address.Add(item, false), 2);
                    // player.InventoryController.AddAndRaiseEvents(item, placement.Address);
                    break;

                case PlacementKind.EquipmentSlot:
                    // D.LogTransaction($"Placing item {item.LocalizedName()} ({item.Id}) in {player.Profile.Nickname} inventory at {placement.Address}");
                    // var slot = player.Equipment.GetSlot(placement.Slot);
                    D.Dump(placement.Address.Add(item, false), 2);
                    // player.InventoryController.AddAndRaiseEvents(item, placement.Address);
                    break;

                case PlacementKind.ArmorPlate:
                    await PlaceArmorPlate(item, player, placement);
                    break;
            }
        }

        private static async UniTask<bool> PlaceArmorPlate(Item item, Player player, ItemPlacement placement)
        {
            D.Dump(placement.Address.AddWithoutRestrictions(item), 2);
            (placement.Address.Container as Slot).ApplyContainedItem();
            return true;
            // var plateHolder = PU.GetPlayerSlotItem(player, placement.Slot) as CompoundItem;
            // var plateHolder = AU.GetPlateHolder(player);

            // var plate = item as ArmorPlateItemClass;
            // foreach (ArmorHolderComponent armorHolder in plateHolder.Components.Where(c => c is ArmorHolderComponent))
            // {
            //     foreach (var slot in armorHolder.ArmorSlots)
            //     {
            //         if (slot.ContainedItem is not null)
            //             continue;
            //         if (slot.CachedSlotName != null && !slot.CachedSlotName.EndsWith("_plate", StringComparison.OrdinalIgnoreCase))
            //             continue;

            //         var addResult = slot.AddWithoutRestrictions(plate);

            //         if (addResult.Failed)
            //         {
            //             // D.Dump(addResult);
            //             return false;
            //         }

            //         // This is an extremely manual way of adding armor
            //         // however after spending an entire day throwing myself against the wall I must give up
            //         // whilst this plate is registered correctly when the player is shot at
            //         // the ui does not display any durability changes
            //         // this is very likely due to me missing a listener somewhere that happens
            //         // in the normal network transaction pipeline
            //         // Sidenote: I could lowkey patch out Slot.Add() specifically for plates to bypass "locked slot" error
            //         plate.CurrentAddress.RaiseAddEvent(plate, CommandStatus.Begin, player.InventoryController);
            //         plate.CurrentAddress.RaiseAddEvent(plate, CommandStatus.Succeed, player.InventoryController);
            //         slot.ApplyContainedItem();

            //         return true;
            //     }
            // }
            // return false;
        }

        // private static async UniTask<bool> PlaceArmorPlate(Item item, Player player, ItemPlacement placement)
        // {
        //     var parentSlot = placement.Address.Container as Slot;
        //     var plate = item as ArmorPlateItemClass;

        //     if (parentSlot is null)
        //     {
        //         D.NotifyLong("Major Error: Can't find a slot to put a plate into");
        //         return false;
        //     }

        //     parentSlot.ApplyContainedItem();
        //     var addResult = parentSlot.AddWithoutRestrictions(item);

        //     if (addResult.Failed)
        //     {
        //         // D.Dump(addResult);
        //         return false;
        //     }

        //     // This is an extremely manual way of adding armor
        //     // however after spending an entire day throwing myself against the wall I must give up
        //     // whilst this plate is registered correctly when the player is shot at
        //     // the ui does not display any durability changes
        //     // this is very likely due to me missing a listener somewhere that happens
        //     // in the normal network transaction pipeline
        //     // Sidenote: I could lowkey patch out Slot.Add() specifically for plates to bypass "locked slot" error
        //     placement.Address.RaiseAddEvent(plate, CommandStatus.Begin, player.InventoryController);
        //     placement.Address.RaiseAddEvent(plate, CommandStatus.Succeed, player.InventoryController);
        //     parentSlot.ApplyContainedItem();

        //     return true;
        // }


        private static void PlayEquipSound(Item item)
        {
            AudioClip clip = H.EFTGUISounds.GetItemClip(item.ItemSound, EInventorySoundType.drop);
            if (clip != null) H.EFTGUISounds.PlaySound(clip);
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
