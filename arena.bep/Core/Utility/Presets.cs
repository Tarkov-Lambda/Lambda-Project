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

        public static void RemovePlates(Player player)
        {
            
        }


        public static void GiveItem(Item item, Player player)
        {
            var slotTypes = GetAppropriateSlot(item, player);
            foreach (var slotType in slotTypes)
            {
                var slot = player.Equipment.GetSlot(slotType);
                if (slot.ContainedItem != null)
                {
                    slot.RemoveItemWithoutRestrictions();
                }

                slot.AddWithoutRestrictions(item);
            }

        }

        public static EquipmentSlot[] GetAppropriateSlot(Item item, Player player)
        {
            EquipmentSlot[] slots = [];
            if (item is Weapon)
            {
                if (item is PistolItemClass)
                {
                    slots.AddItem(EquipmentSlot.Holster);
                }
                else
                {
                    slots.AddItem(EquipmentSlot.FirstPrimaryWeapon);
                }
            }
            else if (item is ArmorPlateItemClass)
            {
                CompoundItem armor = GetPlateHolder(player);
            }
            else if (item is MagazineItemClass)
            {

            }

            return slots;
        }

        public static CompoundItem GetPlateHolder(Player player)
        {

            CompoundItem tacRig = Preset.GetSlot(EquipmentSlot.ArmorVest).ContainedItem as CompoundItem;
            CompoundItem armor = Preset.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as CompoundItem;

            if (armor != null)
            {
                return armor;
            }
            if (tacRig != null && tacRig is ArmorItemClass)
            {
                return tacRig;
            }

            return null;
        }

        /// <summary>
        /// spawn and equip a specific player (we have to know the template id first)
        /// </summary>
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
