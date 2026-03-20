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

                var itemsToRemove = new List<Item>();

                // ── 1. Primary weapons ───────────────────────────────────────────────────
                var primarySlot = player.Equipment.GetSlot(EquipmentSlot.FirstPrimaryWeapon);
                if (primarySlot.ContainedItem != null) itemsToRemove.Add(primarySlot.ContainedItem);

                var secondarySlot = player.Equipment.GetSlot(EquipmentSlot.SecondPrimaryWeapon);
                if (secondarySlot.ContainedItem != null) itemsToRemove.Add(secondarySlot.ContainedItem);

                // ── 2. Pistol – keep only the faction default; track if we need to give it ─
                var holsterSlot = player.Equipment.GetSlot(EquipmentSlot.Holster);
                bool needsDefaultPistol;

                if (holsterSlot.ContainedItem != null)
                {
                    bool isDefault = defaultPistolBsgId != null && holsterSlot.ContainedItem.TemplateId == defaultPistolBsgId;
                    if (!isDefault) itemsToRemove.Add(holsterSlot.ContainedItem);

                    needsDefaultPistol = !isDefault;
                }
                else
                {
                    // Holster was already empty
                    needsDefaultPistol = true;
                }

                // ── 3. Helmet ────────────────────────────────────────────────────────────
                var helmetSlot = player.Equipment.GetSlot(EquipmentSlot.Headwear);
                if (helmetSlot.ContainedItem != null) itemsToRemove.Add(helmetSlot.ContainedItem);

                // ── 5. Rig grid – remove everything except default-pistol magazines (including plates) ──────
                var tacRig = player.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as CompoundItem;
                if (tacRig != null)
                {
                    foreach (var grid in tacRig.Containers)
                    {
                        foreach (var item in grid.Items)
                        {
                            bool isDefaultPistolMag = item is MagazineItemClass mag && defaultPistolMagTemplateId != null && mag.TemplateId == defaultPistolMagTemplateId;
                            if (!isDefaultPistolMag) itemsToRemove.Add(item);
                        }
                    }
                }

                // ── 5. Armor Vest - Remove plates ──────
                var armorVest = player.Equipment.GetSlot(EquipmentSlot.ArmorVest).ContainedItem as CompoundItem;
                if (tacRig != null)
                {
                    foreach (var grid in tacRig.Containers)
                    {
                        foreach (var item in grid.Items)
                        {
                            bool isDefaultPistolMag = item is MagazineItemClass mag && defaultPistolMagTemplateId != null && mag.TemplateId == defaultPistolMagTemplateId;
                            if (!isDefaultPistolMag) itemsToRemove.Add(item);
                        }
                    }
                }

                foreach (var grid in PlayerUtils.GetPlayerPockets(H.MainPlayer).Containers)
                {
                    foreach (var item in grid.Items)
                    {
                        bool isDefaultPistolMag = item is MagazineItemClass mag && defaultPistolMagTemplateId != null && mag.TemplateId == defaultPistolMagTemplateId;
                        if (!isDefaultPistolMag) itemsToRemove.Add(item);
                    }
                }

                // ── Remove collected items one by one ────────────────────────────────────
                foreach (var item in itemsToRemove)
                {
                    await ItemsUtils.TryRemoveItem(item, player);
                    await UniTask.Delay(25);
                }

                // ── 6. Give default pistol if the holster ended up empty ─────────────────
                if (needsDefaultPistol && defaultPistolBsgId != null)
                {
                    var defaultPistolItem = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(defaultPistolBsgId);
                    if (defaultPistolItem != null) await ItemsUtils.ClientRequestGiveItem(defaultPistolItem);
                }

            }
            finally
            {
                IsResetting = false;
            }
        }
    }
}
