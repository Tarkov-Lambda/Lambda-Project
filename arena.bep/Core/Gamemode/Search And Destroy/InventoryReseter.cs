using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.UI;
using ifp.arena.shared;

namespace ifp.arena.bep.Core.Gamemode
{
    public static class InventoryResetter
    {
        public static bool IsResetting { get; private set; }

        public static string GetDefaultPistolBsgId(Faction faction)
        {
            foreach (var category in BuyMenu.buyCategories)
            {
                foreach (var shopItem in category.items)
                {
                    if (string.IsNullOrEmpty(shopItem.ammoId))
                        continue;

                    var immutable = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(shopItem.bsgId);
                    if (immutable is not PistolItemClass)
                        continue;

                    if (shopItem.faction == faction || shopItem.faction == Faction.None)
                        return shopItem.bsgId;
                }
            }

            return null;
        }


        private static string GetDefaultPistolMagTemplateId(string defaultPistolBsgId)
        {
            if (defaultPistolBsgId == null)
                return null;

            var pistol = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(defaultPistolBsgId) as Weapon;
            return pistol?.GetCurrentMagazine()?.TemplateId;
        }

        public static async UniTask ResetInventory()
        {
            var player = H.MainPlayer;
            if (player == null)
                return;

            IsResetting = true;
            try
            {
                H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();

                Faction faction = H.MainPlayerScore?.faction ?? Faction.None;
                string defaultPistolBsgId = GetDefaultPistolBsgId(faction);
                string defaultPistolMagTemplateId = GetDefaultPistolMagTemplateId(defaultPistolBsgId);

                List<Item> itemsToRemove = new List<Item>();

                Item primaryWeapon = player.Equipment.GetSlot(EquipmentSlot.FirstPrimaryWeapon).ContainedItem;
                if (primaryWeapon != null) itemsToRemove.Add(primaryWeapon);

                Item secondaryWeapon = player.Equipment.GetSlot(EquipmentSlot.SecondPrimaryWeapon).ContainedItem;
                if (secondaryWeapon != null) itemsToRemove.Add(secondaryWeapon);

                var pistol = player.Equipment.GetSlot(EquipmentSlot.Holster).ContainedItem;
                bool needsDefaultPistol;

                if (pistol != null)
                {
                    bool isDefault = defaultPistolBsgId != null && pistol.TemplateId == defaultPistolBsgId;
                    if (!isDefault) itemsToRemove.Add(pistol);

                    needsDefaultPistol = !isDefault;
                }
                else
                {
                    needsDefaultPistol = true;
                }

                Item helmetSlot = player.Equipment.GetSlot(EquipmentSlot.Headwear).ContainedItem;
                if (helmetSlot != null) itemsToRemove.Add(helmetSlot);

                // remove anything that isn't default pistol mag (including plates)
                CompoundItem tacRig = player.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as CompoundItem;
                if (tacRig != null)
                {
                    foreach (var grid in tacRig.Containers)
                    {
                        foreach (Item item in grid.Items)
                        {
                            bool isDefaultPistolMag = item is MagazineItemClass mag && defaultPistolMagTemplateId != null && mag.TemplateId == defaultPistolMagTemplateId;
                            if (!isDefaultPistolMag) itemsToRemove.Add(item);
                        }
                    }
                }

                // same as above
                foreach (var grid in PlayerUtils.GetPlayerPockets(H.MainPlayer).Containers)
                {
                    foreach (var item in grid.Items)
                    {
                        bool isDefaultPistolMag = item is MagazineItemClass mag && defaultPistolMagTemplateId != null && mag.TemplateId == defaultPistolMagTemplateId;
                        if (!isDefaultPistolMag) itemsToRemove.Add(item);
                    }
                }

                CompoundItem armorVest = player.Equipment.GetSlot(EquipmentSlot.ArmorVest).ContainedItem as CompoundItem;
                if (armorVest != null)
                {
                    foreach (var armorHolder in armorVest.Components.OfType<ArmorHolderComponent>())
                    {
                        foreach (var slot in armorHolder.ArmorSlots)
                        {
                            if (slot.ContainedItem != null) itemsToRemove.Add(slot.ContainedItem);
                        }
                    }
                }

                // If the currently equipped item doesn't match the recorded preset, remove it.
                foreach (var kvp in PresetManager.Instance.RecordedItems)
                {
                    var currentItem = player.Equipment.GetSlot(kvp.Key).ContainedItem;
                    if (currentItem != null && currentItem.TemplateId != kvp.Value.TemplateId) itemsToRemove.Add(currentItem);
                }



                foreach (var item in itemsToRemove)
                {
                    await ItemsUtils.TryRemoveItem(item, player); // we can prolly run async and forget here
                    await UniTask.Delay(25);
                }

                // give default pistol if needed
                if (needsDefaultPistol && defaultPistolBsgId != null)
                {
                    var defaultPistolItem = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(defaultPistolBsgId);
                    if (defaultPistolItem != null) await ItemsUtils.ClientRequestGiveItem(defaultPistolItem); // we aren't actually awaiting when approved, which is okay ish?
                }


                var presetItems = PresetManager.Instance.RecordedItems;
                foreach (var kvp in presetItems)
                {
                    var currentItem = player.Equipment.GetSlot(kvp.Key).ContainedItem;
                    if (currentItem == null || currentItem.TemplateId != kvp.Value.TemplateId)
                    {
                        await ItemsUtils.ClientRequestGiveItem(kvp.Value);
                        await UniTask.Delay(25);
                    }
                }

            }
            finally
            {
                IsResetting = false;
            }
        }
    }
}
