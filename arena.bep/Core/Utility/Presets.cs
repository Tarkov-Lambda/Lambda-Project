using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;

// --------------------------------------------- //
using SearchableGrid = GClass3117;
using EquipmentBuildClass = GClass3953;
using ifp.arena.bep.Core.Economy;
using ifp.arena.shared;
using ifp.arena.bep.Core.UI; // Assumption
// --------------------------------------------- //


namespace ifp.arena.bep.Core
{
    public static class PresetUtils
    {
        public static WeaponBuildsStorageClass WeaponBuilds => Singleton<ClientApplication<ISession>>.Instance.Session.WeaponBuildsStorage;
        public static EquipmentBuildsStorageClass EquipmentBuilds => Singleton<ClientApplication<ISession>>.Instance.Session.EquipmentBuildsStorage;

        public static IEnumerable<EquipmentBuildClass> Builds => EquipmentBuilds.EquipmentBuilds.Values;
        public static IEnumerable<WeaponBuildClass> Templates => WeaponBuilds.Dictionary_0.Values;
        public static InventoryEquipment Preset => GetDefaultPreset();

        /// <summary>Resets main player equipment to a starting gun, and no armor</summary>
        public static void ResetEquipment()
        {
            
            // H.MainPlayer.InventoryController.RemoveActiveEvent();
        }

        // Retrieves first custom hideout preset
        public static InventoryEquipment GetDefaultPreset()
        {
            foreach (EquipmentBuildClass equipmentTemplate in Builds.ToArray())
            {
                if (equipmentTemplate.BuildType == EFT.Builds.EEquipmentBuildType.Custom)
                {
                    return equipmentTemplate.Equipment;
                }
            }

            return null;
        }

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
            if (BuyMenu.TryGetItemData(weapon.TemplateId, out ShopItem weaponData))
            {  

                ammo = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(weaponData.ammoId) as AmmoItemClass;
                return true;
            }

            ammo = null;
            return false;
        }

 
        // This shit needs to get the fuck outta here
        public static bool CanEnterRaid(out string[] reasons)
        {
            reasons = [];
            var tacRig = Preset.GetSlot(EquipmentSlot.ArmorVest).ContainedItem;
            var armor = Preset.GetSlot(EquipmentSlot.TacticalVest).ContainedItem;

            if (tacRig == null)
            {
                reasons.AddItem("You must have a rig equipped.");
            }
            if (armor == null && tacRig != null && tacRig is not ArmorItemClass)
            {
                reasons.AddItem("You must equip armor or an armored rig.");
            }

            foreach (string reason in reasons)
            {
                H.NotifyLong("You must modify your hideout equipment preset:");
                H.NotifyLong(reason);
            }

            if (reasons.Length > 0) return false;
            return true;
        }
    }
}
