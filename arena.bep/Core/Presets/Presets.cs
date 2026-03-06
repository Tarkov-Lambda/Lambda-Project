using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using ifp.arena.bep.Core;

namespace ifp.arena.Core
{
    public static class ItemSpawner
    {
        public static IEnumerable<GClass3953> Builds => H.EquipmentBuilds.EquipmentBuilds.Values;
        public static IEnumerable<WeaponBuildClass> Templates => H.WeaponBuilds.Dictionary_0.Values;

        public static InventoryEquipment Preset => GetDefaultPreset();

        // Retrieves first custom preset
        public static InventoryEquipment GetDefaultPreset()
        {
            var equipmentBuilds = Builds.ToArray();
            foreach (GClass3953 equipmentBuild in equipmentBuilds)
            {
                if (equipmentBuild.BuildType == EFT.Builds.EEquipmentBuildType.Custom)
                {
                    return equipmentBuild.InventoryEquipment_0;
                }
            }

            return null;
        }

        public static WeaponBuildClass FindCustomTemplate(Item templateItem)
        {
            Templates.Where((build) =>
            {
                return build.Item.Id == templateItem.Id;
            });
            return null;
        }

        public static void EquipItem(string templateId, EquipmentSlot slotType)
        {
            if (PlayerUtils.TryCreateItem(templateId, out Item item))
            {
                var slot = H.MainPlayer.Equipment.GetSlot(slotType);
                if (slot.ContainedItem != null)
                {
                    slot.RemoveItemWithoutRestrictions();
                }
                slot.AddWithoutRestrictions(item);
            }
        }

        public static void BuyItem(Item item)
        {
            if (item is Weapon)
            {

            }
            else if (item is ArmorPlateItemClass)
            {

            }
        }
    }
}