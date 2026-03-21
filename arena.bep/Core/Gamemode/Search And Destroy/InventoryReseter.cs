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

                // Primary / secondary weapons
                Item primaryWeapon = PlayerUtils.GetPlayerSlotItem(player, EquipmentSlot.FirstPrimaryWeapon);
                if (primaryWeapon != null) itemsToRemove.Add(primaryWeapon);

                Item secondaryWeapon = PlayerUtils.GetPlayerSlotItem(player, EquipmentSlot.SecondPrimaryWeapon);
                if (secondaryWeapon != null) itemsToRemove.Add(secondaryWeapon);

                // Keep holster only if it already holds the default pistol
                var pistol = PlayerUtils.GetPlayerSlotItem(player, EquipmentSlot.Holster);
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

                // Helmet
                Item helmetSlot = PlayerUtils.GetPlayerSlotItem(player, EquipmentSlot.Headwear);
                if (helmetSlot != null) itemsToRemove.Add(helmetSlot);

                // Remove everything from vest + pockets that isn't the default pistol mag
                CompoundItem tacRig = PlayerUtils.GetPlayerSlotItem(player, EquipmentSlot.TacticalVest) as CompoundItem;
                foreach (var item in PlayerUtils.GetVestAndPocketGridItems<Item>(player, tacRig))
                {
                    bool isDefaultPistolMag = item is MagazineItemClass mag && defaultPistolMagTemplateId != null && mag.TemplateId == defaultPistolMagTemplateId;
                    if (!isDefaultPistolMag) itemsToRemove.Add(item);
                }

                // Remove all armor plates via the shared helper (covers both armored tac-rigs and armor vests)
                itemsToRemove.AddRange(ItemsUtils.GetArmorPlates(player));

                // If the currently equipped item doesn't match the recorded preset, remove it.
                foreach (var kvp in PresetManager.Instance.RecordedItems)
                {
                    var currentItem = PlayerUtils.GetPlayerSlotItem(player, kvp.Key);
                    if (currentItem != null && currentItem.TemplateId != kvp.Value.TemplateId) itemsToRemove.Add(currentItem);
                }

                await ItemsUtils.TryRemoveItems(itemsToRemove, player);

                // Give default pistol if needed
                if (needsDefaultPistol && defaultPistolBsgId != null)
                {
                    var defaultPistolItem = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(defaultPistolBsgId);
                    if (defaultPistolItem != null) await ItemsUtils.ClientRequestGiveItem(defaultPistolItem);
                }

                var presetItems = PresetManager.Instance.RecordedItems;
                foreach (var kvp in presetItems)
                {
                    var currentItem = PlayerUtils.GetPlayerSlotItem(player, kvp.Key);
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
