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

        public static void AddItem(ref List<Item> itemList, Item item)
        {
            D.LogInventory($"Adding {item.LocalizedName()} ({item.Id}) to removal list");
            itemList.Add(item);
        }

        public static void AddRange(ref List<Item> itemList, IEnumerable<Item> itemCollection)
        {
            foreach (Item item in itemCollection)
            {
                D.LogInventory($"Adding {item.LocalizedName()} ({item.Id}) to removal list");
            }
            itemList.AddRange(itemCollection);
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
                Item primaryWeapon = PU.GetPlayerSlotItem(player, EquipmentSlot.FirstPrimaryWeapon);
                if (primaryWeapon != null) AddItem(ref itemsToRemove, primaryWeapon);

                Item secondaryWeapon = PU.GetPlayerSlotItem(player, EquipmentSlot.SecondPrimaryWeapon);
                if (secondaryWeapon != null) AddItem(ref itemsToRemove, secondaryWeapon);

                // Keep holster only if it already holds the default pistol
                var pistol = PU.GetPlayerSlotItem(player, EquipmentSlot.Holster);
                bool needsDefaultPistol;
                if (pistol != null)
                {
                    bool isDefault = defaultPistolBsgId != null && pistol.TemplateId == defaultPistolBsgId;
                    if (!isDefault) AddItem(ref itemsToRemove, pistol);
                    needsDefaultPistol = !isDefault;
                }
                else
                {
                    needsDefaultPistol = true;
                }

                // Helmet
                Item helmetSlot = PU.GetPlayerSlotItem(player, EquipmentSlot.Headwear);
                if (helmetSlot != null) AddItem(ref itemsToRemove, helmetSlot);

                VestItemClass tacRig = PU.GetPlayerSlotItem(player, EquipmentSlot.TacticalVest) as VestItemClass;
                // if (tacRig != null && AU.IsTacRigArmored(tacRig))
                // {
                //     AddItem(ref itemsToRemove, tacRig);
                // }
                // else
                // {
                // Remove everything from vest + pockets that isn't the default pistol mag
                foreach (var item in PU.GetVestAndPocketGridItems<Item>(player, tacRig))
                {
                    bool isDefaultPistolMag = item is MagazineItemClass mag && defaultPistolMagTemplateId != null && mag.TemplateId == defaultPistolMagTemplateId;
                    if (!isDefaultPistolMag) AddItem(ref itemsToRemove, item);
                }
                // }

                ArmorItemClass armorVest = PU.GetPlayerSlotItem(player, EquipmentSlot.ArmorVest) as ArmorItemClass;
                if (armorVest != null && armorVest != PresetManager.Instance.RecordedItems[EquipmentSlot.ArmorVest])
                {
                    AddItem(ref itemsToRemove, armorVest);
                }


                // If the currently equipped item doesn't match the recorded preset, remove it.
                if (PresetManager.Instance != null)
                {
                    foreach (var kvp in PresetManager.Instance.RecordedItems)
                    {
                        var currentItem = PU.GetPlayerSlotItem(player, kvp.Key);
                        if (currentItem != null && kvp.Value != null && currentItem.TemplateId != kvp.Value.TemplateId) AddItem(ref itemsToRemove, currentItem);
                    }
                }

                await IU.TryPopItems(itemsToRemove, player);


                // GIVING

                if (needsDefaultPistol && defaultPistolBsgId != null)
                {
                    var defaultPistolItem = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(defaultPistolBsgId);
                    if (defaultPistolItem != null) await IU.ClientRequestGiveItem(defaultPistolItem);
                }

                var presetItems = PresetManager.Instance?.RecordedItems;
                if (presetItems != null)
                {
                    foreach (var kvp in presetItems)
                    {
                        var currentItem = PU.GetPlayerSlotItem(player, kvp.Key);
                        if (kvp.Value != null && (currentItem == null || currentItem.TemplateId != kvp.Value.TemplateId))
                        {
                            await UniTask.Delay(25);
                            await IU.ClientRequestGiveItem(kvp.Value);
                        }
                    }
                }

                // plate removal in case the player just got a fresh plate carrier
                List<Item> platesToRemove = new List<Item>();
                AddRange(ref platesToRemove, AU.GetArmorPlates(player));

                foreach (Item plateToRemove in platesToRemove)
                {
                    IU.ClientRequestPopItem(plateToRemove);
                    await UniTask.Delay(25);
                }
            }
            finally
            {
                IsResetting = false;
            }
        }
    }
}
