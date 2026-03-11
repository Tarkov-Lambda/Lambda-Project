using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using ifp.arena.bep.networking;
using System.Linq;
using System;
using EFT.UI;
using UnityEngine;

using SearchableGrid = GClass3117;
using ItemExtensions = GClass3380;
using AddItemEventArgs = GEventArgs2;
using RefreshItemEventArgs = GEventArgs18;
using RemoveItemEventArgs = GEventArgs3;
using Cysharp.Threading.Tasks;

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


        public static async UniTask<bool> ClientRequestGiveItem(Item templateItem)
        {
            var item = CloneItem(templateItem);
            if (item == null) return false;

            if (item is Weapon)
            {
                var places = GetAppropriateSlot(item, H.MainPlayer);
                Slot gunSlot;

                if (places.slots.Count > 0)
                {
                    gunSlot = H.MainPlayer.Inventory.Equipment.GetSlot(places.slots.First());
                }
                else
                {
                    H.Notify("Can't find a slot");
                    return false;
                }

                Weapon existingWeapon = gunSlot.ContainedItem as Weapon;

                if (existingWeapon != null)
                {
                    // if the gun slot we are about to replace is equipped
                    // unequip and discard the weapon
                    if (H.MainPlayer.HandsController.Item.CurrentAddress == existingWeapon.CurrentAddress)
                    {
                        var asdas = H.MainPlayer.InventoryController.TryThrowItem(existingWeapon);
                        await UniTask.Delay(200);
                    }
                }
            }

            Singleton<SpawnItemPacketHandler>.Instance.Send(item);
            return true;
        }

        public static void WhenApprovedGiveItem(Item item, Player player)
        {
            var places = GetAppropriateSlot(item, player);

            if (places.itemAddress != null)
            {
                player.InventoryController.AddAndRaiseEvents(item, places.itemAddress);
            }

            foreach (var slotType in places.slots)
            {
                var slot = player.Equipment.GetSlot(slotType);
                if (slot.ContainedItem != null)
                {
                    slot.RemoveItemWithoutRestrictions();
                }

                player.InventoryController.AddAndRaiseEvents(item, slot.CreateItemAddress());
            }
            if (item is Weapon weapon)
            {
                if (PresetUtils.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
                {
                    PlayerUtils.ReplenishGun(weapon, ammo);
                    PlayerUtils.ReplenishVestMagazines(weapon, ammo, player);
                }

                FireModeComponent firemode = weapon.Components.Find(component => component is FireModeComponent) as FireModeComponent;
                if (firemode != null && firemode.AvailableEFireModes.Contains(Weapon.EFireMode.fullauto))
                {
                    firemode.FireMode = Weapon.EFireMode.fullauto;
                }
            }

            if (player.IsYourPlayer)
            {
                AudioClip itemClip = Singleton<GUISounds>.Instance.GetItemClip(item.ItemSound, EInventorySoundType.drop);
                if (itemClip != null)
                {
                    Singleton<GUISounds>.Instance.PlaySound(itemClip);
                }
            }
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

                foreach (var slot in armor.AllSlots)
                {
                    // H.Dump(slot);
                    foreach (var childItem in slot.Items)
                    {
                            H.Dump(childItem);
                        if (childItem is ArmoredEquipmentItemClass plate)
                        {
                            H.Dump(plate);
                            // armor.Repairable.Durability = armor.Repairable.MaxDurability;
                        }
                        // places.slots.Add(armor.Slots.); // front plate
                        // places.slots.Add(armor.Slots.); // back plate
                    }
                }
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
            VestItemClass tacRig = player.Inventory.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as VestItemClass;
            // ArmorItemClass armor = player.Inventory.Equipment.GetSlot(EquipmentSlot.ArmorVest).ContainedItem as ArmorItemClass;
            // H.Dump(tacRig);
            if (tacRig != null && tacRig.Slots.Count() > 0)
            {
                return tacRig;
            }
            // else if (armor != null)
            // {
            //     return armor;
            // }

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
