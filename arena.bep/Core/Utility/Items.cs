using SearchableGrid = GClass3117;
using ItemExtensions = GClass3380;
using AddItemEventArgs = GEventArgs2;
using RefreshItemEventArgs = GEventArgs18;
using RemoveItemEventArgs = GEventArgs3;
using OperationResult = GStruct153;
//---------------------------------------------------------------//

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
using Cysharp.Threading.Tasks;
using System.Security.Cryptography;
using Diz.LanguageExtensions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// This file is primarily used for dealing with allocating space in the player's inventory, sending spawn requests, and then spawning the objects.
// ClientRequestGiveItem sees if it has everything needed on its end
// if it's successful, it will request SpawnItem, which if approved will load bundles and then execute WhenApprovedGiveItem (on every client)
// The current setup is as following:
// Any primary weapon will drop whatever is in the first weapon slot
// Any secondary weapon will do the same for holster
// Anything like grenades, medical item, magazines find an address that they can go to in the rig
// Helmet goes to headwear
//
// Here's where it gets slightly janky:
// ArmorPlateItem
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
            if (templateItem == null)
                return false;

            var places = GetAppropriateSlot(templateItem, H.MainPlayer);

            var areAllSlotsFree = true;

            // synchronous but whatever for now
            foreach (var slotType in places.slotTypes)
            {
                // very janky if condition considering we set GetAppropriateSlot to tacrig or armor for further logic, which needs a major rework
                if (templateItem is not ArmorPlateItemClass)
                {
                    var slot = H.MainInventory.Equipment.GetSlot(slotType);
                    if (slot.ContainedItem is not null)
                    {
                        var removalResult = false;
                        if (templateItem is BackpackItemClass)
                        {
                            removalResult = await TryRemoveSlot(slotType);
                        }
                        else
                        {
                            GStruct156<bool> result = H.MainInventoryController.TryThrowItem(slot.ContainedItem);
                            removalResult = result.Succeeded;
                        }

                        if (removalResult is false)
                        {
                            H.Notify("Failed to allocate slot space in the inventory.");
                            return false;
                        }

                        await UniTask.Delay(300);
                    }
                }
            }

            if (areAllSlotsFree)
            {
                await UniTask.Delay(300);
                var item = CloneItem(templateItem);
                Singleton<SpawnItemPacketHandler>.Instance.Send(item);
                return true;
            }

            return false;
        }


        public static async UniTask<bool> TryRemoveSlot(EquipmentSlot equipmentSlot)
        {
            var slot = H.MainPlayer.Inventory.Equipment.GetSlot(equipmentSlot);
            if (slot.ContainedItem == null)
                return true;

            OperationResult removalEvent = InteractionsHandlerClass.Remove(slot.ContainedItem, H.MainInventoryController, true);
            if (removalEvent.Failed)
            {
                return false;
            }
            else
            {
                IResult transactionResult = await H.MainPlayer.InventoryController.TryRunNetworkTransaction(removalEvent);
                H.Dump(transactionResult);
                H.Dump(transactionResult.Error);
                H.Dump(transactionResult.Succeed);
                if (transactionResult.Failed)
                {
                    return false;
                }
            }

            return true;
        }

        public static async UniTask DelayAndGiveBombToAPlayer()
        {
            await UniTask.Delay(50);
            Singleton<BombAssignmentPacketHandler>.Instance.Send();
        }

        public static async void WhenApprovedGiveItem(Item item, Player player)
        {
            var places = GetAppropriateSlot(item, player);

            if (places.itemAddress != null)
            {
                player.InventoryController.AddAndRaiseEvents(item, places.itemAddress);
            }

            foreach (var slotType in places.slotTypes)
            {
                if (item is ArmorPlateItemClass)
                {
                    CompoundItem armor = GetPlateHolder(player);
                    foreach (ArmorHolderComponent armorHolder in armor.Components.Where(component => component is ArmorHolderComponent))
                    {
                        foreach (var slot in armorHolder.ArmorSlots)
                        {
                            if (slot.ContainedItem is null)
                            {
                                if (slot.CachedSlotName is "Front_plate" or "Back_plate")
                                {
                                    GStruct153 add = slot.AddWithoutRestrictions(item);
                                    if (add.Succeeded)
                                    {
                                        var result = await player.InventoryController.TryRunNetworkTransaction(add);
                                        break;
                                    }
                                    else
                                    {
                                        H.Dump(add);
                                    }
                                }

                                // RepairItem()
                            }
                        }
                    }
                }
                else
                {
                    var slot = player.Equipment.GetSlot(slotType);
                    if (slot.ContainedItem != null)
                    {
                        slot.RemoveItemWithoutRestrictions();
                    }

                    player.InventoryController.AddAndRaiseEvents(item, slot.CreateItemAddress());
                }
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
            public List<EquipmentSlot> slotTypes; // For equipment
            public ItemAddress itemAddress; // For singular stuff like mags or nades
        }

        // Monolithic router of items (we kinda have to do this garbage in order to do stuff like instant armor plate equips)
        public static Places GetAppropriateSlot(Item item, Player player)
        {
            Places places = new Places
            {
                slotTypes = new List<EquipmentSlot>(),
            };


            if (item is Weapon)
            {
                if (item is PistolItemClass)
                {
                    places.slotTypes.Add(EquipmentSlot.Holster);
                }
                else
                {
                    places.slotTypes.Add(EquipmentSlot.FirstPrimaryWeapon);
                }
            }
            else if (item is BackpackItemClass)
            {
                places.slotTypes.Add(EquipmentSlot.Backpack);
            }
            else if (item is ArmorPlateItemClass)
            {
                VestItemClass tacRig = player.Inventory.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as VestItemClass;
                // ArmorItemClass armor = player.Inventory.Equipment.GetSlot(EquipmentSlot.ArmorVest).ContainedItem as ArmorItemClass;
                // && tacRig.Components.Count(c => c is ArmorHolderComponent) > 0
                if (tacRig != null)
                {
                    places.slotTypes.Add(EquipmentSlot.TacticalVest);

                }
                // else if (armor != null)
                // {
                //     places.slotTypes.Add(EquipmentSlot.ArmorVest);
                // }
            }
            else if (item is HeadwearItemClass)
            {
                places.slotTypes.Add(EquipmentSlot.Headwear);
            }
            else if (item is MagazineItemClass or MedicalItemClass or ThrowWeapItemClass or BarterItemItemClass or KeycardItemClass)
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
            ArmorItemClass armor = player.Inventory.Equipment.GetSlot(EquipmentSlot.ArmorVest).ContainedItem as ArmorItemClass;
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
