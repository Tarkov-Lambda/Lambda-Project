using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.UI;
using ifp.arena.shared.Models;

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

    // Fetch a build that exists in the user's gun builds
    // priority: selected -> any user made build -> bsg made
    public static WeaponBuildClass GetCustomTemplate(string bsgId)
    {
        if (WeaponPresetManager.Instance.SelectedGunPreset.TryGetValue(bsgId, out var mongoId))
        {
            var matchByMongo = WeaponPresets.FirstOrDefault(b => b.MongoID_0 == mongoId);
            if (matchByMongo != null)
                return matchByMongo;
        }

        var userBuild = WeaponPresets.FirstOrDefault(b => !b.FromPreset && b.Item?.TemplateId == bsgId);

        if (userBuild != null)
            return userBuild;

        return WeaponPresets.FirstOrDefault(b => b.Item?.TemplateId == bsgId);
    }

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
}
