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

    public static WeaponBuildsStorageClass WeaponBuilds => Singleton<ClientApplication<ISession>>.Instance.Session.WeaponBuildsStorage;
    public static EquipmentBuildsStorageClass EquipmentBuilds => Singleton<ClientApplication<ISession>>.Instance.Session.EquipmentBuildsStorage;

    public static IEnumerable<EquipmentBuildClass> Builds => EquipmentBuilds.EquipmentBuilds.Values;
    public static IEnumerable<WeaponBuildClass> Templates => WeaponBuilds.Dictionary_0.Values;

    // Fetch a build that exists in the user's gun builds (defaults to stock preset)
    public static WeaponBuildClass GetCustomTemplate(string bsgId)
    {
        return Templates.FirstOrDefault((build) =>
        {
            return build.Item.TemplateId == bsgId;
        });
    }

    public static bool TryGetGunAmmo(Weapon weapon, out AmmoItemClass ammo)
    {
        if (BuyMenuSelection.TryGetItemData(weapon.TemplateId, out ShopItem weaponData))
        {
            ammo = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(weaponData.ammoId) as AmmoItemClass;
            return true;
        }

        ammo = null;
        return false;
    }
}
