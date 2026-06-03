using System.Collections.Generic;
using System.Threading;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Networking;
using UnityEngine;
using Cysharp.Threading.Tasks;
using EFT.Interactive;
using Lambda.Core.Main.UI;
using Lambda.Shared.Models;
using Lambda.Core.Main.Economy;
using System.Linq;

namespace Lambda.Core.Main;

// 1. ClientRequestGiveItem client checks it can make room, then sends SpawnItemPacket
// 2. SpawnItemPacketWarden server approves, broadcasts to all clients, loads bundles, executes WhenApprovedGiveItem
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
                default
            );
        }
    }

    public static async UniTask<bool> ClientRequestBuyItem(Item templateItem)
    {
        if (!PassesPrePurchaseChecks(templateItem))
            return false;

        await _lock.WaitAsync();

        try
        {
            var clonedItem = templateItem.CloneItem();
            var placement = AU.GetItemPlacement(clonedItem, H.MainPlayer);

            if (placement.Kind == PlacementKind.None)
            {
                D.LogError("Local player can not find a placement for an item, aborting.");
                return false;
            }

            bool isSlotCleared = await TryClearEquipmentSlotAsync(clonedItem, placement);
            if (!isSlotCleared)
            {
                D.Notify("Failed to allocate slot space in the inventory.");
                return false;
            }

            if (placement.Kind is not PlacementKind.ArmorPlate && clonedItem is not BackpackItemClass)
            {
                var addResult = placement.Address.Add(clonedItem, true);
                if (addResult.Failed)
                {
                    D.Notify(addResult.Error_0);
                    return false;
                }
            }

            clonedItem.StackObjectsCount = 1;

#if DEBUG
            D.LogTransaction($"{H.MainPlayer.Profile.Nickname} requesting {clonedItem.LocalizedShortName()} ({clonedItem.Id}) at ({placement.Address})");
#endif

            StripArmorPlatesIfNeeded(clonedItem);

            Singleton<BuyItemPacketWarden>.Instance.Send(clonedItem, placement, H.MainPlayer);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool PassesPrePurchaseChecks(Item templateItem)
    {
        if (H.IsHeadless || templateItem == null)
            return false;

        if (H.MainPlayer.MovementContext.CurrentState is not IdleStateClass)
        {
            D.Notify("You can't buy when moving");
            return false;
        }

        return true;
    }

    private static async UniTask<bool> TryClearEquipmentSlotAsync(Item clonedItem, ItemPlacement placement)
    {
        if (placement.Kind != PlacementKind.EquipmentSlot)
            return true;

        var slot = H.MainInventory.Equipment.GetSlot(placement.Slot);
        if (slot.ContainedItem == null)
            return true; // Slot is already empty

        if (clonedItem is BackpackItemClass)
        {
            Singleton<ForceRemoveItemPacketWarden>.Instance.Send(slot.ContainedItem);
            return true;
        }

        if (clonedItem is VestItemClass or ArmorItemClass)
        {
            return await H.MainPlayer.TryPopContainedItem(placement.Slot);
        }

        D.LogInventory("Trying to remove an item");

        bool isWeapon = clonedItem is Weapon;

        if (H.Gamemode is IGMRespawnable)
        {
            return isWeapon
                ? await H.MainPlayer.TryPopWeaponAndMags(placement.Slot)
                : await H.MainPlayer.TryPopContainedItem(placement.Slot);
        }
        else
        {
            return isWeapon
                ? await H.MainPlayer.TryThrowWeaponAndMags(placement.Slot)
                : await H.MainPlayer.TryThrowContainedItem(placement.Slot);
        }
    }

    public static void StripArmorPlatesIfNeeded(Item clonedItem)
    {
        if (clonedItem is ArmorItemClass armorItem)
        {
            foreach (var plate in armorItem.GetArmorPlates())
            {
                plate.CurrentAddress.RemoveWithoutRestrictions(plate);
            }
        }
        else if (clonedItem is VestItemClass vestItem && vestItem.IsTacRigArmored())
        {
            foreach (var plate in vestItem.GetArmorPlates())
            {
                plate.CurrentAddress.RemoveWithoutRestrictions(plate);
            }
        }
    }

    public static void DowngradeMagIfNeeded(Weapon weapon)
    {
        if (BuyMenuSelection.TryGetItemData(weapon.TemplateId, out ShopItem itemData))
        {
            var magSlot = weapon.GetMagazineSlot();
            MagazineItemClass mag = magSlot.ContainedItem as MagazineItemClass;

            bool needsReplacement = mag == null || mag.Cartridges.MaxCount > 40 || mag.Cartridges.Items.Any(cartridge => cartridge.TemplateId != itemData.ammoId);

            if (needsReplacement)
            {
                WeaponBuildClass defaultPresetWeaponBuild = FU.Presets.FirstOrDefault(b => b.FromPreset && b.Item.TemplateId == weapon.TemplateId);
                Weapon defaultPresetWeapon = defaultPresetWeaponBuild.Item as Weapon;

                MagazineItemClass defaultWeaponMag = defaultPresetWeapon.GetCurrentMagazine().CloneItem();

                if (mag != null)
                {
                    magSlot.RemoveItemWithoutRestrictions();
                }
                magSlot.AddWithoutRestrictions(defaultWeaponMag);
            }
        }
    }

    public static void AttachNightVisionIfNeeded(HeadwearItemClass headwear)
    {
        if (!H.IsNightTime) return;

        foreach (var slot in headwear.Slots)
        {
            if (slot.Name == "mod_nvg")
            {
                string targetNvgId = (headwear.TemplateId == Hardcode.STRAP_NVG) ? Hardcode.N15 : Hardcode.GPNVG;
                Item nvg = PresetItemsCache.Instance.GetPresetItem(targetNvgId).CloneItem();

                TogglableComponent togglableComponent = nvg.GetItemComponent<TogglableComponent>();
                togglableComponent?.ForceToggle(true);

                slot.AddWithoutRestrictions(nvg);
                break;
            }
        }
    }

    public static void AddArmbandIfNeeded(Player player)
    {
        if (H.Gamemode is IGMTeam)
        {
            string selectedArmband = player.GetContext().Faction == Faction.CT ? Hardcode.ARMBAND_CT : Hardcode.ARMBAND_T;

            ArmBandItemClass armband = PresetItemsCache.Instance.GetPresetItem(selectedArmband).CloneItem() as ArmBandItemClass;

            var armbandSlot = player.Equipment.GetSlot(EquipmentSlot.ArmBand);

            if (armbandSlot.ContainedItem != null)
            {
                armbandSlot.ContainedItem.CurrentAddress.RemoveWithoutRestrictions(armbandSlot.ContainedItem);
            }

            armbandSlot.CreateItemAddress().AddWithoutRestrictions(armband);
        }
    }

    public static void GarbageCollectWorldLoot()
    {
        ObservedLootItem[] allLoot = GameObject.FindObjectsByType<ObservedLootItem>(FindObjectsSortMode.None);
        // ObservedSmokeGrenade[] allSmokes = GameObject.FindObjectsByType<ObservedSmokeGrenade>(FindObjectsSortMode.None);

        foreach (ObservedLootItem loot in allLoot)
        {
            if (!loot.isActiveAndEnabled) continue;
            loot.Kill();
        }

        // foreach (ObservedSmokeGrenade smoke in allSmokes)
        // {
        //     Object.Destroy(smoke);
        // }
    }
}