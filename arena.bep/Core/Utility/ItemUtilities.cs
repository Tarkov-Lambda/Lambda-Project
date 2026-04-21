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
using ifp.arena.bep.Core.FX;

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
            await H.PoolManagerClass.LoadBundlesAndCreatePools(
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
        if (H.IsHeadless) return false;

        // await _lock.WaitAsync();

        // try
        // {
            if (templateItem == null)
                return false;

            var placement = AU.GetItemPlacement(templateItem, H.MainPlayer);
            if (placement.Kind == PlacementKind.None)
            {
                D.LogError("Local player can not find a placement for an item, aborting.");
                return false;
            }

            if (placement.Kind == PlacementKind.EquipmentSlot)
            {

                var slot = H.MainInventory.Equipment.GetSlot(placement.Slot);
                if (slot.ContainedItem != null)
                {
                    bool removed;
                    if (templateItem is BackpackItemClass)
                    {
                        removed = true;
                        Singleton<ForceRemoveItemPacketHandler>.Instance.Send(slot.ContainedItem);
                    }
                    else if (templateItem is VestItemClass or ArmorItemClass)
                    {
                        removed = await H.MainPlayer.TryPopContainedItem(placement.Slot);
                    }
                    else
                    {
                        D.LogInventory("Trying to remove an item");
                        if (templateItem is Weapon)
                            removed = await H.MainPlayer.TryThrowWeaponAndMags(placement.Slot);
                        else
                            removed = await H.MainPlayer.TryThrowContainedItem(placement.Slot);
                    }

                    if (!removed)
                    {
                        D.Notify("Failed to allocate slot space in the inventory.");
                        return false;
                    }
                }
            }

            if (placement.Kind is not PlacementKind.ArmorPlate && templateItem is not BackpackItemClass)
            {
                var addResult = placement.Address.Add(templateItem, true);
                if (addResult.Failed)
                {
                    D.Notify(addResult.Error_0);
                    return false;
                }
            }

            templateItem.StackObjectsCount = 1;

#if DEBUG
            D.LogTransaction($"{H.MainPlayer.Profile.Nickname} requesting {templateItem.LocalizedShortName()} ({templateItem.Id}) at ({placement.Address})");
#endif

            if (templateItem is ArmorItemClass armorItem)
            {
                foreach (var plate in armorItem.GetArmorPlates())
                {
                    plate.CurrentAddress.RemoveWithoutRestrictions(plate);
                }
            }
            else if (templateItem is VestItemClass vestItem)
            {
                if (vestItem.IsTacRigArmored())
                {
                    foreach (var plate in vestItem.GetArmorPlates())
                    {
                        plate.CurrentAddress.RemoveWithoutRestrictions(plate);
                    }
                }
            }

            Singleton<BuyItemPacketHandler>.Instance.Send(templateItem, placement);
            return true;
        // }
        // finally
        // {
        //     // await UniTask.Delay(100);
        //     _lock.Release();
        // }
    }

    public static void ClientRequestPopItem(Item item)
    {
        Singleton<ForceRemoveItemPacketHandler>.Instance.Send(item);
        // IU.TryPopItemWithoutRestriction(item, item.CurrentAddress, H.MainPlayer).Forget();
    }


    public static void WhenApprovedGiveItem(Item item, Player player, ItemPlacement placement)
    {
        player.PlaceItem(item, placement);

        if (item is Weapon weapon) RU.SetupWeaponAfterEquip(weapon, player);
        if (player.IsYourPlayer) AudioHandler.PlayEquipSound(item);
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