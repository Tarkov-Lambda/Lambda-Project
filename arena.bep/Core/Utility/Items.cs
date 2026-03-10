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
using Fika.Core.Main.Players;
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

        public static void ClientRequestGiveItem(Item templateItem)
        {
            var item = CloneItem(templateItem);
            if (item == null) return;

            Singleton<SpawnItemPacketHandler>.Instance.Send(item);
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
                weapon.Components.All((component) =>
                {
                    H.Notify(component.GetType().ToString());
                    return true;
                });
                FireModeComponent firemode = weapon.Components.Find(component => component is FireModeComponent) as FireModeComponent;
                if (firemode != null && firemode.AvailableEFireModes.Contains(Weapon.EFireMode.fullauto))
                {
                    firemode.FireMode = Weapon.EFireMode.fullauto;
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
                    // H.Notify(slot.LocalizedName());
                }
                // places.slots.Add(armor.Slots.); // front plate
                // places.slots.Add(armor.Slots.); // back plate
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
