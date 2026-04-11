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
using Fika.Core.Main.Players;
using HarmonyLib;
using System.Reflection;

namespace ifp.arena.bep.Core;

// 1. ClientRequestGiveItem client checks it can make room, then sends SpawnItemPacket
// 2. SpawnItemPacketHandler server approves, broadcasts to all clients, loads bundles, executes WhenApprovedGiveItem
// 3. WhenApprovedGiveItem every client places the item in the correct slot/address (for each player on the server)
public static class ItemUtilities
{
    private static readonly SemaphoreSlim _lock = new(1, 1);

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
        var prefabsToLoad = new List<ResourceKey>();

        foreach (var i in item.GetAllItems())
        {
            if (i.Template == null)
                continue;

            var prefab = i.Template.Prefab;

            if (prefab != null && !string.IsNullOrEmpty(prefab.path))
            {
                prefabsToLoad.Add(prefab);
            }
        }

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
        await _lock.WaitAsync();
        try
        {
            if (templateItem == null)
                return false;

            var placement = AU.GetItemPlacement(templateItem, H.MainPlayer);

            if (placement.Kind == PlacementKind.EquipmentSlot)
            {
                var slot = H.MainInventory.Equipment.GetSlot(placement.Slot);
                if (slot.ContainedItem != null)
                {
                    bool removed;
                    if (templateItem is BackpackItemClass or VestItemClass or ArmorItemClass)
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

            if (placement.Kind is not PlacementKind.ArmorPlate)
            {
                var addResult = placement.Address.Add(templateItem, true);
                if (addResult.Failed)
                {
                    D.Notify(addResult.Error_0);
                    return false;
                }
            }

            Item clonedItem = templateItem.CloneItem(H.MainPlayer.InventoryController);
            clonedItem.StackObjectsCount = 1;
            D.LogTransaction($"{H.MainPlayer.Profile.Nickname} requesting {clonedItem.LocalizedShortName()} ({clonedItem.Id}) at ({placement.Address})");
            Singleton<SpawnItemPacketHandler>.Instance.Send(clonedItem, placement);
            return true;
        }
        finally
        {
            await UniTask.Delay(25);
            _lock.Release();
        }
    }

    public static void ClientRequestPopItem(Item item)
    {
        Singleton<RemoveItemPacketHandler>.Instance.Send(item);
        // IU.TryPopItemWithoutRestriction(item, item.CurrentAddress, H.MainPlayer).Forget();
    }


    public static async UniTask WhenApprovedGiveItem(Item item, Player player, ItemPlacement placement)
    {
        await UniTask.Delay(25);
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
            await UniTask.WaitUntil(() =>
                player.MovementContext.CurrentState is IdleStateClass ||
                player.MovementContext.CurrentState is not SprintStateClass && player.MovementContext.Velocity.sqrMagnitude == 0f);
            if (extraBackpackWait && equipmentSlot == EquipmentSlot.Backpack)
            {
                await PU.WaitUntilStationary(player);

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
        return await TryDoItemAction(item, player, InteractionsHandlerClass.Remove, "remove");
    }

    // public static async UniTask TryPopItemsWithoutRestriction(IEnumerable<Item> items, Player player, int delayMs = 25)
    // {
    //     foreach (var item in items)
    //     {
    //         await TryPopItemWithoutRestriction(item, player);
    //         if (delayMs != 0) await UniTask.Delay(delayMs);
    //     }
    // }

    public static async UniTask<bool> TryPopItemWithoutRestriction(Item item, ItemAddress itemAddress, Player player)
    {
        itemAddress.RemoveWithoutRestrictions(item);

        itemAddress.RaiseRemoveEvent(item, CommandStatus.Begin, player.InventoryController);
        itemAddress.RaiseRemoveEvent(item, CommandStatus.Succeed, player.InventoryController);

        FikaPlayer fikaPlayer = player as FikaPlayer;
        MethodInfo method = AccessTools.Method(typeof(FikaPlayer), "RecalculateEquippedArmorComponents");
        method.Invoke(fikaPlayer, [AU.GetPlateCarrier(player)]);

        return true;
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

    public static async UniTask<bool> TryDoItemActionWithoutRestriction<T>(
        Item item,
        Player player,
        Func<Item, InventoryController, GStruct154<T>> action, string actionName) where T : IRaiseEvents
    {
        D.LogInventory($"Player {player.Profile.Nickname} is trying to {actionName} {item.LocalizedName()} ({item.Id})");
        var address = item.CurrentAddress;
        var opResult = action(item, player.InventoryController);

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

        IEnumerable<MagazineItemClass> magsToThrow = null;
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


    private static async UniTask<bool> PlaceItem(Item item, Player player, ItemPlacement placement)
    {
        D.LogTransaction($"{player.Profile.Nickname} adding {item.LocalizedShortName()} ({item.Id}) to ({placement.Address})");

        switch (placement.Kind)
        {
            case PlacementKind.VestAddress:
            case PlacementKind.EquipmentSlot:
                // if (player.IsYourPlayer)
                // {
                player.InventoryController.AddAndRaiseEvents(item, placement.Address);
                // }
                // else
                // {
                //     placement.Address.Add(item, false);
                // }
                break;

            case PlacementKind.ArmorPlate:
                placement.Address.AddWithoutRestrictions(item);
                break;
        }

        placement.Address.RaiseAddEvent(item, CommandStatus.Begin, player.InventoryController);
        placement.Address.RaiseAddEvent(item, CommandStatus.Succeed, player.InventoryController);

        if (placement.Kind == PlacementKind.ArmorPlate)
        {
            FikaPlayer fikaPlayer = player as FikaPlayer;
            MethodInfo method = AccessTools.Method(typeof(FikaPlayer), "RecalculateEquippedArmorComponents");
            method.Invoke(fikaPlayer, [AU.GetPlateCarrier(player)]);
        }

        return true;
    }

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