using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using Fika.Core;
using Fika.Core.Main.Players;
using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using UnityEngine;

namespace ifp.arena.bep.Core;

public static class InventoryActionExtensions
{
    // THIS MUST ONLY BE CALLED WHEN THE PLAYER IS STANDING STILL
    // OTHERWISE THE INVENTORY CONTROLLER GETS LOCKED OUT FOREVER
    public static async UniTask<bool> TryPopContainedItem(this Player player, EquipmentSlot equipmentSlot, bool waitUntilStationary = false)
    {
        return await player.TryOperateOnSlot(equipmentSlot, TryPopItem, waitUntilStationary, extraBackpackWait: true);
    }

    public static async UniTask<bool> TryThrowContainedItem(this Player player, EquipmentSlot equipmentSlot, bool waitUntilStationary = false)
    {
        return await player.TryOperateOnSlot(equipmentSlot, TryThrowItem, waitUntilStationary);
    }

    private static async UniTask<bool> TryOperateOnSlot(
        this Player player,
        EquipmentSlot equipmentSlot,
        Func<Player, Item, UniTask<bool>> operation,
        bool waitUntilStationary,
        bool extraBackpackWait = false)
    {
        Item item = player.GetSlotItem(equipmentSlot);
        if (item == null) return true;

        // if (waitUntilStationary)
        // {
        //     await UniTask.WaitUntil(() => player.MovementContext.CurrentState is IdleStateClass);
        //     if (extraBackpackWait && equipmentSlot == EquipmentSlot.Backpack)
        //     {
        //         await PU.WaitUntilStationary(player);
        //     }
        // }

        return await operation(player, item);
    }

    public static async UniTask TryPopItems(this Player player, IEnumerable<Item> items, int delayMs = 25)
    {
        foreach (var item in items)
        {
            await player.TryPopItem(item);
            if (delayMs != 0) await UniTask.Delay(delayMs);
        }
    }

    public static async UniTask<bool> TryPopItem(this Player player, Item item)
    {
        return await player.TryDoItemAction(item, InteractionsHandlerClass.Remove, "remove");
    }

    public static async UniTask TryThrowItems(this Player player, IEnumerable<Item> items, int delayMs = 25)
    {
        foreach (var item in items)
        {
            await player.TryThrowItem(item);
            // if (delayMs != 0) await UniTask.Delay(delayMs);
        }
    }

    public static UniTask<bool> TryThrowItem(this Player player, Item item)
    {
        return player.TryDoItemAction(item, InteractionsHandlerClass.Throw, "throw");
    }

    public static async UniTask<bool> TryDoItemAction<T>(
        this Player player,
        Item item,
        Func<Item, InventoryController, bool, GStruct154<T>> action, string actionName) where T : IRaiseEvents
    {
#if DEBUG
        D.LogInventory($"Player {player.Profile.Nickname} is trying to {actionName} {item.LocalizedName()} ({item.Id})");
#endif

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

            // full retard mode right here
            if (result.Error.StartsWith("Could not find"))
            {
                var cachedAddress = item.CurrentAddress;
                cachedAddress.RemoveWithoutRestrictions(item);

                cachedAddress.RaiseRemoveEvent(item, CommandStatus.Begin, player.InventoryController);
                cachedAddress.RaiseRemoveEvent(item, CommandStatus.Succeed, player.InventoryController);
                return true;
            }
        }

        return !result.Failed;
    }

    public static bool TryPopItemWithoutRestriction(this Player player, Item item, ItemAddress itemAddress)
    {
        var removeResult = itemAddress.RemoveWithoutRestrictions(item);
        if (removeResult.Failed)
        {
            D.LogError($"Failed to pop item without restriction: {removeResult.Error}");
            return false;
        }

        itemAddress.RaiseRemoveEvent(item, CommandStatus.Begin, player.InventoryController);
        itemAddress.RaiseRemoveEvent(item, CommandStatus.Succeed, player.InventoryController);

        if (item is ArmorPlateItemClass)
            player.TryRecalculateEquippedArmorComponents();

        return true;
    }

    public static bool TryRecalculateEquippedArmorComponents(this Player player, CompoundItem plateCarrier = null)
    {
        try
        {
            plateCarrier ??= player.GetPlateCarrier();

            if (plateCarrier == null)
            {
                D.LogError($"Can't find the plate carrier in {player.Profile.Nickname}'s inventory to place the armor plate into");
                return false;
            }

            FikaPlayer fikaPlayer = player as FikaPlayer;
            MethodInfo method = AccessTools.Method(typeof(FikaPlayer), "RecalculateEquippedArmorComponents");
            method.Invoke(fikaPlayer, [plateCarrier]);
            return true;
        }
        catch (Exception e)
        {
            D.LogError($"An error has occured trying to Recalculate Equipped Armor Components for {player.Profile.Nickname}");
            D.Log(e.Message);
            D.Log(e.StackTrace);
            return false;
        }
    }

    public static async UniTask<bool> TryDoItemActionWithoutRestriction<T>(
        this Player player,
        Item item,
        Func<Item, InventoryController, GStruct154<T>> action, string actionName) where T : IRaiseEvents
    {
#if DEBUG
        D.LogInventory($"Player {player.Profile.Nickname} is trying to {actionName} {item.LocalizedName()} ({item.Id})");
#endif

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

    public static async UniTask<bool> TryThrowWeaponAndMags(this Player player, EquipmentSlot equipmentSlot)
    {
        var slot = player.Inventory.Equipment.GetSlot(equipmentSlot);
        if (slot.ContainedItem == null) return true;

        IEnumerable<MagazineItemClass> magsToThrow = null;
        if (slot.ContainedItem is Weapon oldWeapon)
        {
            string oldMagTemplateId = oldWeapon.GetCurrentMagazine()?.TemplateId;
            if (oldMagTemplateId != null)
            {
                var vest = player.GetSlotItem(EquipmentSlot.TacticalVest) as CompoundItem;
                magsToThrow = player.GetMatchingMags(oldMagTemplateId, vest)?.ToList();
            }
        }

        if (magsToThrow != null)
        {
            await player.TryThrowItems(magsToThrow, 25);
        }

        bool removed = await player.TryThrowContainedItem(equipmentSlot, false);

        return removed;
    }

    public static bool PlaceItem(this Player player, Item item, ItemPlacement placement)
    {
#if DEBUG
        D.LogTransaction($"{player.Profile.Nickname} adding {item.LocalizedShortName()} ({item.Id}) to ({placement.Address})");
#endif

        var addResult = placement.Address.AddWithoutRestrictions(item);
        if (addResult.Failed)
        {
            D.LogError($"Failed to add item {item.Id} to {placement.Address}. Reason: {addResult.Error}");
            return false;
        }

        player.AutoExamineAndSearch(item);

        placement.Address.RaiseAddEvent(item, CommandStatus.Begin, player.InventoryController);
        placement.Address.RaiseAddEvent(item, CommandStatus.Succeed, player.InventoryController);

        if (placement.Kind == PlacementKind.ArmorPlate) player.TryRecalculateEquippedArmorComponents();

        return true;
    }

    public static void ForceUnlockInventory(this Player player)
    {
        try
        {
            player.InventoryController.List_0?.Clear();

            if (player is FikaPlayer fikaPlayer && fikaPlayer.OperationCallbacks.Count > 0)
            {
                D.LogInventory($"Clearing {fikaPlayer.OperationCallbacks.Count} stalled OperationCallbacks on {player.Profile.Nickname}");
                fikaPlayer.OperationCallbacks.Clear();
            }

            var allItems = player.InventoryController.Inventory.GetPlayerItems(EPlayerItems.All);
            foreach (var item in allItems)
            {
                if (item.PinLockState != EItemPinLockState.Free)
                    item.PinLockState = EItemPinLockState.Free;
                if (item.TryGetItemComponent(out LockableComponent lockable))
                    lockable.Locked = false;
            }
        }
        catch (Exception ex) { D.LogError($"Error unlocking inventory: {ex}"); }
    }

    private static void AutoExamineAndSearch(this Player player, Item rootItem)
    {
        var searchController = player.InventoryController.SearchController;
        Type searchType = searchController?.GetType();

        // 6. BRUTEFORCE REFLECTION: 
        // The SearchController tracks "SearchedItems", "KnownItems", and "SearchedContainers" in private HashSets/Dictionaries.
        // iterate over its fields and forcefully inject our item IDs into any collection we find.
        if (searchController != null && searchType != null)
        {
            var fields = AccessTools.GetDeclaredFields(searchType);
            foreach (var field in fields)
            {
                var value = field.GetValue(searchController);
                if (value == null) continue;

                if (value is HashSet<string> hashSetStr)
                {
                    foreach (var item in rootItem.GetAllItems())
                        hashSetStr.Add(item.Id.ToString());
                }
                else if (value is HashSet<MongoID> hashSetMongo)
                {
                    foreach (var item in rootItem.GetAllItems())
                        hashSetMongo.Add(item.Id);
                }
                else if (value is Dictionary<string, bool> dictStr)
                {
                    foreach (var item in rootItem.GetAllItems())
                        dictStr[item.Id.ToString()] = true;
                }
                else if (value is Dictionary<MongoID, bool> dictMongo)
                {
                    foreach (var item in rootItem.GetAllItems())
                        dictMongo[item.Id] = true;
                }
                else if (value is HashSet<Item> hashSetItem)
                {
                    foreach (var item in rootItem.GetAllItems())
                        hashSetItem.Add(item);
                }
            }
        }
    }
}