using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.UI;
using ifp.arena.shared.Models;
using Newtonsoft.Json;

namespace ifp.arena.bep.Core;

/// <summary>
/// Factory Utilities
/// Has pointers for Tarkov's Singleton instances
/// </summary>
public static class FactoryUtilities
{
    public static ItemFactoryClass ItemFactory => Singleton<ItemFactoryClass>.Instance;

    public static WeaponBuildsStorageClass WeaponBuildsStorage => Singleton<ClientApplication<ISession>>.Instance.Session.WeaponBuildsStorage;
    public static EquipmentBuildsStorageClass EquipmentBuildsStorage => Singleton<ClientApplication<ISession>>.Instance.Session.EquipmentBuildsStorage;

    // I assume this is like armor/headwear type shit
    public static IEnumerable<EquipmentBuildClass> EquipmentPresets => EquipmentBuildsStorage.EquipmentBuilds.Values;
    // BSG or user made weapon presets.
    public static IEnumerable<WeaponBuildClass> WeaponPresets => WeaponBuildsStorage.Dictionary_0.Values;

    public static List<string> GetAllWeaponTemplateIds()
    {
        List<string> weaponTemplateIds = ItemFactory.ItemTemplates.Values
            .OfType<WeaponTemplate>()
            .Select(template => template._id.ToString())
            .ToList();

        // List<string> weaponTemplateIds = null;

        return weaponTemplateIds;
    }

    public static bool TryGetGunAmmo(Weapon weapon, out AmmoItemClass ammo)
    {
        if (BuyMenuSelection.TryGetItemData(weapon.TemplateId, out ShopItem weaponData))
        {
            ammo = Singleton<PresetItemsCache>.Instance.GetPresetItem(weaponData.ammoId) as AmmoItemClass;
            return true;
        }

        ammo = null;
        return false;
    }

    public static async UniTaskVoid SendDelayed(string serializedItem, string presetName, int delayMs = 3000)
    {
        await UniTask.Delay(delayMs);
        Singleton<WeaponPresetManager>.Instance.UpdateSelectedPreset(serializedItem, presetName);
    }


    public static async UniTask CreateAndSaveWeaponPreset(Item weapon, string presetName)
    {
        var existingPreset = WeaponBuildsStorage.FindByName(presetName);
        if (existingPreset != null) return;

        MongoID newPresetId = new MongoID(Guid.NewGuid().ToString("N").Substring(0, 24));

        // The constructor automatically clones the weapon (item.CloneItemWithSameId<Item>()) 
        // it should safely detach, but I'm regenerating all ID's as a secondary measure just in case down below
        WeaponBuildClass newPreset = new WeaponBuildClass(
            id: newPresetId,
            itemIconName: string.Empty,
            handbookName: presetName,
            item: weapon,
            fromPreset: false
        );

        // this calls backend via ISession.SaveWeaponBuild and method_2() to register it locally if successful.
        IResult result = await WeaponBuildsStorage.SaveBuild(newPreset);
        if (result.Succeed)
        {
            D.Log($"Successfully created and saved weapon preset: {presetName}");
        }
        else
        {
            D.LogError($"Failed to save weapon preset: {result.Error}");
        }
    }

    public static string SerializeItem(Item item)
    {
        FlatItemsDataClass[] flatItems = ItemFactory.TreeToFlatItems(item);
        return JsonConvert.SerializeObject(flatItems, Formatting.Indented);
    }

    public static Item InstantiatePreset(string json)
    {
        var flatItems = JsonConvert.DeserializeObject<FlatItemsDataClass[]>(json);
        if (flatItems == null || flatItems.Length == 0) return null;

        // 1. Remap IDs
        var idMap = new Dictionary<string, string>();
        foreach (var item in flatItems)
        {
            idMap[item._id] = ItemFactory.MongoID_0;
        }

        // 2. Assign New IDs and update Parents
        string rootId = null;
        foreach (var item in flatItems)
        {
            string oldId = item._id;
            string oldParentId = item.parentId;

            item._id = idMap[oldId];

            if (!string.IsNullOrEmpty(oldParentId) && idMap.ContainsKey(oldParentId))
            {
                item.parentId = idMap[oldParentId];
            }
            else
            {
                // This is our root item
                item.parentId = null;
                item.slotId = null;
                rootId = item._id;
            }
        }

        // 3. Reconstruct the tree
        var result = ItemFactory.FlatItemsToTree(flatItems, silentMode: false);

        // 4. Safety Checks
        if (result.DeserializationErrors.Count > 0)
        {
            foreach (var error in result.DeserializationErrors)
            {
                UnityEngine.Debug.LogError($"[Preset Error] {error}");
            }
        }

        // 5. Try to get the root item safely
        if (rootId != null && result.Items.TryGetValue(rootId, out Item rootItem))
        {
            return rootItem;
        }

        // If we reach here, the root item failed to create. 
        // Let's find out why by checking if the template exists in the factory.
        string originalTpl = flatItems.FirstOrDefault(x => x._id == rootId)?._tpl;
        D.LogError($"Failed to find root item {rootId} in created items. Template was: {originalTpl}");

        return null;
    }

    public static Item DeserializeItem(string json)
    {
        var flatItems = JsonConvert.DeserializeObject<FlatItemsDataClass[]>(json);
        var result = ItemFactory.FlatItemsToTree(flatItems, silentMode: true);

        if (result.DeserializationErrors.Count > 0)
        {
            return null;
        }
        string rootId = flatItems[0]._id;
        return result.Items[rootId];
    }
}
