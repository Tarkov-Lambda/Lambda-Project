using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using Diz.Resources;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking.Packets.Debug;
using HarmonyLib;
using ifp.arena.bep.networking;

namespace ifp.arena.bep.Core
{
    public static class PresetUtils
    {
        public static ItemFactoryClass ItemFactory => Singleton<ItemFactoryClass>.Instance;
        // public static ItemFactoryClass ItemFactory => Singleton<ItemFactoryClass>.Instance;


        public static WeaponBuildsStorageClass WeaponBuilds => Singleton<ClientApplication<ISession>>.Instance.Session.WeaponBuildsStorage;
        public static EquipmentBuildsStorageClass EquipmentBuilds => Singleton<ClientApplication<ISession>>.Instance.Session.EquipmentBuildsStorage;

        public static IEnumerable<GClass3953> Builds => EquipmentBuilds.EquipmentBuilds.Values;
        public static IEnumerable<WeaponBuildClass> Templates => WeaponBuilds.Dictionary_0.Values;
        public static InventoryEquipment Preset => GetDefaultPreset();

        public static void SpawnItem(Item item)
        {
            // var presetGun = GetCustomTemplate(item);

            Singleton<SpawnItemPacketHandler>.Instance.Send(item);

            // var slot = H.MainPlayer.Equipment.GetSlot(slotType);
            // if (slot.ContainedItem != null)
            // {
            //     slot.RemoveItemWithoutRestrictions();
            // }

            // slot.AddWithoutRestrictions(newItem);
        }

        public static void GiveItem(Item item, Player player)
        {
            EquipmentSlot slotType = item is PistolItemClass ? EquipmentSlot.Holster : EquipmentSlot.FirstPrimaryWeapon;
            
            var slot = player.Equipment.GetSlot(slotType);
            if (slot.ContainedItem != null)
            {
                slot.RemoveItemWithoutRestrictions();
            }

            slot.AddWithoutRestrictions(item);
        }

        public static void PreloadItemAssets(Item item)
        {
            var resourceKey = item.Template;

        }

        public static void SpawnAndEquip(string templateId, EquipmentSlot slotType)
        {
            SpawnAndEquip(H.MainPlayer, templateId, slotType);
        }

        public static void SpawnAndEquip(Player player, string templateId, EquipmentSlot slotType)
        {
            if (TryCreateItem(templateId, out Item item))
            {
                var slot = player.Equipment.GetSlot(slotType);
                if (slot.ContainedItem != null)
                {
                    slot.RemoveItemWithoutRestrictions();
                }
                slot.AddWithoutRestrictions(item);
            }
        }

        // Retrieves first custom hideout preset
        public static InventoryEquipment GetDefaultPreset()
        {
            foreach (GClass3953 equipmentTemplate in Builds.ToArray())
            {
                if (equipmentTemplate.BuildType == EFT.Builds.EEquipmentBuildType.Custom)
                {
                    return equipmentTemplate.Equipment;
                }
            }

            return null;
        }

        // Fetch a build that exists in the user's gun builds (defaults to stock preset)
        public static WeaponBuildClass GetCustomTemplate(Item templateItem)
        {
            return Templates.First((build) =>
            {
                return build.Item.Id == templateItem.Id;
            });
        }

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


        public static bool TryCreateItem(string templateId, out Item newItem)
        {
            newItem = null;

            newItem = ItemFactory.CreateItem(MongoID.Generate(), templateId, itemDiff: null);
            return newItem != null;
        }

        public static Item CreateItem(string templateId)
        {
            TryCreateItem(templateId, out Item newItem);
            return newItem;
        }
    }
}
