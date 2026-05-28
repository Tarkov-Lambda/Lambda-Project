using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Players;
using HarmonyLib;
using Lambda.Core.Main;
using Lambda.Core.Networking;
using static Fika.Core.Main.ClientClasses.ClientInventoryController;

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
        }

        return !result.Failed;
    }

    public static bool TryPopItemWithoutRestriction(this Player player, Item item, ItemAddress itemAddress)
    {
        if (player.HandsController != null && player.HandsController.Item == item)
        {
            player.HandsController.Destroy();
            player.HandsController = null;
        }

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

    static readonly MethodInfo RecalculateEquippedArmorComponents = AccessTools.Method(typeof(FikaPlayer), "RecalculateEquippedArmorComponents");
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

            RecalculateEquippedArmorComponents.Invoke(player as FikaPlayer, [plateCarrier]);
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

    private static async UniTask<bool> TryOperateWeaponAndMags(
        this Player player,
        EquipmentSlot equipmentSlot,
        Func<Player, Item, UniTask<bool>> itemOperation,
        Func<Player, IEnumerable<Item>, UniTask> multiItemOperation)
    {
        var slot = player.Inventory.Equipment.GetSlot(equipmentSlot);
        if (slot.ContainedItem == null) return true;

        IEnumerable<Item> mags = null;

        if (slot.ContainedItem is Weapon weapon)
        {
            string magTemplateId = weapon.GetCurrentMagazine()?.TemplateId;
            if (magTemplateId != null)
            {
                var vest = player.GetSlotItem(EquipmentSlot.TacticalVest) as CompoundItem;
                mags = player.GetMatchingMags(magTemplateId, vest)?.Cast<Item>().ToList();
            }
        }

        // operate on mags first
        if (mags != null)
        {
            await multiItemOperation(player, mags);
        }

        // then operate on weapon itself
        return await itemOperation(player, slot.ContainedItem);
    }

    public static async UniTask<bool> TryThrowWeaponAndMags(this Player player, EquipmentSlot equipmentSlot)
    {
        // TODO: stop being a retard
        if (H.IsServer && player.IsYourPlayer)
        {
            if (player.HandsController.Item == player.GetSlotItem(equipmentSlot))
            {
                player.UnfuckHands();
                await UniTask.Delay(50);
            }
        }

        return await player.TryOperateWeaponAndMags(
            equipmentSlot,
            (p, item) => p.TryThrowItem(item),
            (p, items) => p.TryThrowItems(items, 25)
        );
    }

    public static async UniTask<bool> TryPopWeaponAndMags(this Player player, EquipmentSlot equipmentSlot)
    {
        return await player.TryOperateWeaponAndMags(
            equipmentSlot,
            (p, item) => p.TryPopItem(item),
            async (p, items) =>
            {
                List<UniTask> poppingTasks = new();
                foreach (var item in items)
                {
                    poppingTasks.Add(p.TryPopItem(item));
                }
                await UniTask.WhenAll(poppingTasks);
            }
        );
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
                var callbacks = fikaPlayer.OperationCallbacks.Values.ToList();
                fikaPlayer.OperationCallbacks.Clear();
                foreach (var cb in callbacks)
                {
                    cb?.Invoke(new ServerOperationStatus(EOperationStatus.Failed, "Forcefully unlocked inventory"));
                }
            }

            if (player.HandsController != null && player.ProcessStatus != Player.EProcessStatus.None)
            {
                player.HandsController.FastForwardCurrentState();
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

    public static void AutoExamineAndSearch(this Player player, Item rootItem)
    {
        if (player.InventoryController.SearchController is not PlayerSearchControllerClass searchController) return;

        var allItems = rootItem.GetAllItems();

        foreach (var item in allItems)
        {
            if (searchController.Dictionary_0.TryGetValue(item, out var value) && value.Equals(item.Parent))
                continue;
            searchController.SetItemAsKnown(item, true);
            if (item is SearchableItemItemClass searchable)
            {
                searchController.SetItemAsSearched(searchable);
            }
        }
    }
}