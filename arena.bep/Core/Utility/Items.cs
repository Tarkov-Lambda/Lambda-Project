using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using ifp.arena.bep.networking;
using Cysharp.Threading.Tasks;
using System.Linq;
using System;

// --------------------------------------------- //
using SearchableGrid = GClass3117;
using ItemExtensions = GClass3380;
using AddItemEventArgs = GEventArgs2;
using RefreshItemEventArgs = GEventArgs18;
using RemoveItemEventArgs = GEventArgs3;
// --------------------------------------------- //

namespace ifp.arena.bep.Core
{
    public static class ItemsUtils
    {
        public static ItemFactoryClass ItemFactory => Singleton<ItemFactoryClass>.Instance;

        public static Item CreateItemFromTemplateId(string templateId)
        {
            return ItemFactory.CreateItem(MongoID.Generate(), templateId, itemDiff: null);
        }

        private static Item CloneItem(Item templateItem)
        {
            return ItemExtensions.CloneItem(templateItem);
        }

        public static void ClientGiveItem(Item templateItem, Player player)
        {
            var item = CloneItem(templateItem);
            if (item == null) return;

            Singleton<SpawnItemPacketHandler>.Instance.Send(item);
        }

        public static void SyncGiveItem(Item item, Player player)
        {
            var places = GetAppropriateSlot(item, player);

            if (places.itemAddress != null)
            {
                places.itemAddress.AddWithoutRestrictions(item);
                RefreshItemInventory(item, player);
            }

            foreach (var slotType in places.slots)
            {
                var slot = player.Equipment.GetSlot(slotType);
                if (slot.ContainedItem != null)
                {
                    var oldItem = slot.ContainedItem;
                    var oldAddress = oldItem.CurrentAddress;

                    slot.RemoveItemWithoutRestrictions();
                    RemoveItemInventory(oldItem, oldAddress, player);
                }

                slot.AddWithoutRestrictions(item);
                RefreshItemInventory(item, player);
            }
        }

        public static Action<GEventArgs2> GetAddAction(Player player)
        {
            var field = AccessTools.Field(player.InventoryController.GetType(), "_packetProcessor");

            return field?.GetValue(player.InventoryController) as Action<GEventArgs2>;
        }

        public static async void RefreshItemInventory(Item item, Player player)
        {
            AddItemEventArgs addArg = new AddItemEventArgs(item, item.CurrentAddress, CommandStatus.Succeed, player.InventoryController);
            player.InventoryController.RaiseAddEvent(addArg);

            // player.InventoryController.method_0(addArg);

            // Action<GEventArgs2> action = GetAddAction(player);
            // action(addArg);

            // await UniTask.Delay(300);
            // RefreshItemEventArgs refreshArg = new RefreshItemEventArgs(item, player.InventoryController, refreshIcon: true, checkMagazine: true);
            // player.InventoryController.RaiseEvent(refreshArg);

        }

        public static void RemoveItemInventory(Item oldItem, ItemAddress oldAddress, Player player)
        {
            RemoveItemEventArgs removeArg = new RemoveItemEventArgs(oldItem, oldAddress, CommandStatus.Succeed, player.InventoryController);
            player.InventoryController.RaiseRemoveEvent(removeArg);
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

            if (item is Weapon)
            {
                if (item is PistolItemClass)
                {
                    places.slots.Add(EquipmentSlot.Holster);
                }
                else
                {
                    places.slots.Add(EquipmentSlot.FirstPrimaryWeapon);
                }
            }
            else if (item is BackpackItemClass)
            {
                places.slots.Add(EquipmentSlot.Backpack);
            }
            else if (item is ArmorPlateItemClass)
            {
                CompoundItem armor = GetPlateHolder(player);
            }
            else if (item is HeadwearItemClass)
            {
                places.slots.Add(EquipmentSlot.Headwear);
            }
            else if (item is MagazineItemClass or MedicalItemClass or ThrowWeapItemClass)
            {
                var vest = player.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as SearchableItemItemClass;

                if (vest != null)
                {
                    foreach (var container in vest.Containers)
                    {
                        if (container is SearchableGrid && container.TryFindLocationForItem(item, out ItemAddress location))
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
