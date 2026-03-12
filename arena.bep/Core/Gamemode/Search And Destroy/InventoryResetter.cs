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
        /// <summary>True while ResetInventory is actively running. Use this to lock out inventory access.</summary>
        public static bool IsResetting { get; private set; }

        /// <summary>
        /// Returns the bsgId of the default pistol for the given faction –
        /// the first PistolItemClass entry in BuyMenu that matches the faction
        /// (or has no faction restriction).
        /// </summary>
        public static string GetDefaultPistolBsgId(Faction faction)
        {
            foreach (var category in BuyMenu.buyCategories)
            {
                foreach (var shopItem in category.items)
                {
                    // Weapons always have an ammoId; skip utility / equipment entries
                    if (string.IsNullOrEmpty(shopItem.ammoId))
                        continue;

                    var immutable = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(shopItem.bsgId);
                    if (immutable is not PistolItemClass)
                        continue;

                    // Faction.None on a ShopItem means "available to everyone"
                    if (shopItem.faction == faction || shopItem.faction == Faction.None)
                        return shopItem.bsgId;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the magazine TemplateId that the default pistol ships with,
        /// derived from the immutable (template) weapon in the items cache.
        /// </summary>
        private static string GetDefaultPistolMagTemplateId(string defaultPistolBsgId)
        {
            if (defaultPistolBsgId == null)
                return null;

            var pistol = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(defaultPistolBsgId) as Weapon;
            return pistol?.GetCurrentMagazine()?.TemplateId;
        }

        /// <summary>
        /// Resets the local player's round-purchased gear while preserving:
        /// <list type="bullet">
        ///   <item>The faction's default pistol (if the player currently holds it)</item>
        ///   <item>Magazines in the rig that belong to that default pistol</item>
        /// </list>
        /// Removed items:
        /// <list type="bullet">
        ///   <item>Primary weapon (FirstPrimaryWeapon slot)</item>
        ///   <item>Holstered pistol – unless it is the faction default</item>
        ///   <item>Helmet (Headwear slot)</item>
        ///   <item>Armor plates (Front_plate / Back_plate slots inside the rig)</item>
        ///   <item>Everything else in the rig grids that is not a default-pistol magazine</item>
        /// </list>
        /// </summary>
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
                if (primarySlot.ContainedItem != null)
                    itemsToRemove.Add(primarySlot.ContainedItem);

                var secondarySlot = player.Equipment.GetSlot(EquipmentSlot.SecondPrimaryWeapon);
                if (secondarySlot.ContainedItem != null)
                    itemsToRemove.Add(secondarySlot.ContainedItem);

                // ── 2. Pistol – keep only the faction default; track if we need to give it ─
                var holsterSlot = player.Equipment.GetSlot(EquipmentSlot.Holster);
                bool needsDefaultPistol;
                if (holsterSlot.ContainedItem != null)
                {
                    bool isDefault = defaultPistolBsgId != null
                        && holsterSlot.ContainedItem.TemplateId == defaultPistolBsgId;

                    if (!isDefault)
                        itemsToRemove.Add(holsterSlot.ContainedItem);

                    needsDefaultPistol = !isDefault;
                }
                else
                {
                    // Holster was already empty
                    needsDefaultPistol = true;
                }

                // ── 3. Helmet ────────────────────────────────────────────────────────────
                var helmetSlot = player.Equipment.GetSlot(EquipmentSlot.Headwear);
                if (helmetSlot.ContainedItem != null)
                    itemsToRemove.Add(helmetSlot.ContainedItem);

                // ── 4. Armor plates (Front_plate / Back_plate inside the rig) ────────────
                itemsToRemove.AddRange(ItemsUtils.GetArmorPlates(player));

                // ── 5. Rig grid – remove everything except default-pistol magazines ──────
                var vest = player.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as CompoundItem;
                if (vest != null)
                {
                    foreach (var grid in vest.Grids)
                    {
                        foreach (var item in grid.Items.ToArray())
                        {
                            bool isDefaultPistolMag = item is MagazineItemClass mag
                                && defaultPistolMagTemplateId != null
                                && mag.TemplateId == defaultPistolMagTemplateId;

                            if (!isDefaultPistolMag)
                                itemsToRemove.Add(item);
                        }
                    }
                }

                // ── Remove collected items one by one ────────────────────────────────────
                foreach (var item in itemsToRemove)
                {
                    await ItemsUtils.TryRemoveItem(item, player);
                    await UniTask.Delay(50);
                }

                // ── 6. Give default pistol if the holster ended up empty ─────────────────
                if (needsDefaultPistol && defaultPistolBsgId != null)
                {
                    var defaultPistolItem = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(defaultPistolBsgId);
                    if (defaultPistolItem != null)
                        await ItemsUtils.ClientRequestGiveItem(defaultPistolItem);
                }

            }
            finally
            {
                IsResetting = false;
            }
        }
    }
}
