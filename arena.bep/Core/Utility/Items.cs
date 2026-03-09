using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using ifp.arena.bep.networking;

//
using SearchableGrid = GClass3117;
//


namespace ifp.arena.bep.Core
{
    public static class ItemsUtils
    {
        public static ItemFactoryClass ItemFactory => Singleton<ItemFactoryClass>.Instance;

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
                RefreshItemInventory(item, player, places.itemAddress);
            }

            foreach (var slotType in places.slots)
            {
                var slot = player.PlayerBody.Equipment.GetSlot(slotType);
                if (slot.ContainedItem != null)
                {
                    slot.RemoveItemWithoutRestrictions();
                }
                slot.AddWithoutRestrictions(item);

                var address = player.Equipment.GetSlot(slotType).CreateItemAddress();
                RefreshItemInventory(item, player, address);
            }
        }

        // When we add without restrictions, the player body model will not update by default
        // requiring the player to invoke RaiseEvents by swapping a weapon for example
        // here, we bypass that and directly call our own raise event
        public static void RefreshItemInventory(Item item, Player player, ItemAddress itemAddress)
        {
            GEventArgs2 refreshArg = new GEventArgs2(item, itemAddress, CommandStatus.Succeed, player.Equipment.Owner);
            player.InventoryController.RaiseAddEvent(refreshArg);
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
            else if (item is MagazineItemClass or MedicalItemClass or ThrowWeapItemClass)
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

            CompoundItem tacRig = PresetUtils.Preset.GetSlot(EquipmentSlot.ArmorVest).ContainedItem as CompoundItem;
            CompoundItem armor = PresetUtils.Preset.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as CompoundItem;

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

        // This shit needs to get the fuck outta here
        public static bool CanEnterRaid(out string[] reasons)
        {
            reasons = [];
            var tacRig = PresetUtils.Preset.GetSlot(EquipmentSlot.ArmorVest).ContainedItem;
            var armor = PresetUtils.Preset.GetSlot(EquipmentSlot.TacticalVest).ContainedItem;

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
