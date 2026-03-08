using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using Cysharp.Threading.Tasks.Triggers;
using Diz.Resources;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using Fika.Core.Networking.Packets.Debug;
using HarmonyLib;
using ifp.arena.bep.networking;


//
using SearchableGrid = GClass3117;
//


namespace ifp.arena.bep.Core
{
    public static class PresetUtils
    {
        public static ItemFactoryClass ItemFactory => Singleton<ItemFactoryClass>.Instance;

        public static WeaponBuildsStorageClass WeaponBuilds => Singleton<ClientApplication<ISession>>.Instance.Session.WeaponBuildsStorage;
        public static EquipmentBuildsStorageClass EquipmentBuilds => Singleton<ClientApplication<ISession>>.Instance.Session.EquipmentBuildsStorage;

        public static IEnumerable<GClass3953> Builds => EquipmentBuilds.EquipmentBuilds.Values;
        public static IEnumerable<WeaponBuildClass> Templates => WeaponBuilds.Dictionary_0.Values;
        public static InventoryEquipment Preset => GetDefaultPreset();

        public static void GiveItem(Item templateItem, Player player)
        {
            var item = CloneItem(templateItem);
            if (item == null) return;

            var places = GetAppropriateSlot(item, H.MainPlayer);

            if (places.itemAddress != null)
            {
                places.itemAddress.AddWithoutRestrictions(item);
            }

            foreach (var slotType in places.slots)
            {
                var slot = H.MainPlayer.Equipment.GetSlot(slotType);
                if (slot.ContainedItem != null)
                {
                    slot.RemoveItemWithoutRestrictions();
                }

                slot.AddWithoutRestrictions(item);
            }

            H.Notify(item.LocalizedName());

            Singleton<SpawnItemPacketHandler>.Instance.Send(item);
        }

        public static Item CreateItemFromTemplateId(string templateId)
        {
            return ItemFactory.CreateItem(MongoID.Generate(), templateId, itemDiff: null);
        }

        public static Item CloneItem(Item templateItem)
        {
            return GClass3380.CloneItem(templateItem);
        }

        public static void SyncGiveItem(Item item, Player player)
        {
            var places = GetAppropriateSlot(item, player);

            if (places.itemAddress != null)
            {
                places.itemAddress.AddWithoutRestrictions(item);
            }

            foreach (var slotType in places.slots)
            {
                var slot = player.PlayerBody.Equipment.GetSlot(slotType);
                if (slot.ContainedItem != null)
                {
                    slot.RemoveItemWithoutRestrictions();
                }

                slot.AddWithoutRestrictions(item);
            }


            // H.Notify(player.Equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem.LocalizedName());
        }

        // refactor-later core
        public struct Places
        {
            public List<EquipmentSlot> slots;
            public ItemAddress itemAddress;
        }

        public static Places GetAppropriateSlot(Item item, Player player)
        {
            Places places = new Places
            {
                slots = new List<EquipmentSlot>()
            };

            if (item is PistolItemClass)
            {
                places.slots.Add(EquipmentSlot.Holster);
            }
            else if (item is AssaultRifleItemClass or MarksmanRifleItemClass or SmgItemClass)
            {
                places.slots.Add(EquipmentSlot.FirstPrimaryWeapon);
            }
            else if (item is BackpackItemClass)
            {
                places.slots.Add(EquipmentSlot.Backpack);
            }
            else if (item is ArmorPlateItemClass)
            {
                CompoundItem armor = GetPlateHolder(player);
            }
            else if (item is FoodItemClass)
            {
                var vest = player.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as SearchableItemItemClass;

                if (vest != null)
                {
                    foreach (var container in vest.Containers)
                    {
                        if (container is SearchableGrid &&
                            container.TryFindLocationForItem(item, out ItemAddress location))
                        {
                            places.itemAddress = location;
                            break;
                        }
                    }
                }
            }

            return places;
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

        public static bool TryCreateItem(string templateId, out Item newItem)
        {
            newItem = ItemFactory.CreateItem(MongoID.Generate(), templateId, itemDiff: null);
            return newItem != null;
        }
    }
}
