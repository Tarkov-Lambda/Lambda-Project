using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using ifp.arena.bep.Core.Gamemode;
using UnityEngine;

namespace ifp.arena.bep.Core
{
    /// <summary>Player Utilities</summary>
    public static class PlayerUtilities
    {
        public static SearchableItemItemClass GetPlayerPockets(Player player) => player.Equipment.GetSlot(EquipmentSlot.Pockets).ContainedItem as SearchableItemItemClass;
        public static Item GetPlayerSlotItem(Player player, EquipmentSlot slotType) => player.Equipment.GetSlot(slotType).ContainedItem;

        public static IEnumerable<T> GetVestAndPocketGridItems<T>(Player player, CompoundItem vest) where T : Item
        {
            var pockets = GetPlayerPockets(player);
            var vestItems = vest?.Grids.SelectMany(g => g.Items) ?? Enumerable.Empty<Item>();
            var pocketItems = pockets?.Grids.SelectMany(g => g.Items) ?? Enumerable.Empty<Item>();
            return vestItems.Concat(pocketItems).OfType<T>();
        }

        public static List<MagazineItemClass> GetMatchingMags(Player player, CompoundItem vest, string magTemplateId) =>
        GetVestAndPocketGridItems<MagazineItemClass>(player, vest).Where(m => m.TemplateId == magTemplateId).ToList();

        public static List<Weapon> GetPlayerWeapons(Player player)
        {
            List<Weapon> weapons = new();
            foreach (var slot in player.Equipment.AllSlots)
            {
                foreach (var item in slot.Items)
                {
                    if (item is Weapon weapon)
                    {
                        weapons.Add(weapon);
                    }
                }
            }
            return weapons;
        }

        public static async Task CloseEyes(bool playDeathAudio = true, bool openAfter = true, int closeDelay = 750, int openDelay = 4500)
        {
            DeathFade deathFade = CameraClass.Instance.Camera.GetComponent<DeathFade>();
            deathFade.enabled = true;

            await Task.Delay(closeDelay);
            deathFade.EnableEffect();

            if (playDeathAudio)
            {
                var resourceRequest = Resources.LoadAsync<UISoundsWrapper>("Audio/UISoundsWrapper");
                var soundsWrapper = (UISoundsWrapper)resourceRequest.asset;
                var uIClip = soundsWrapper.GetUIClip(EUISoundType.PlayerIsDead);

                H.EFTGUISounds.PlaySound(uIClip, false, true);
                H.EFTGUISounds.PlayUISound(EUISoundType.PlayerIsDead);
            }

            if (openAfter)
            {
                await Task.Delay(openDelay);
                if (H.Arena.ActiveRules is SND_ModeRules)
                {
                    // H.SpectatorManager.SpectatePlayer()
                }
                OpenEyes();
            }
        }

        public static void OpenEyes()
        {
            DeathFade deathFade = CameraClass.Instance.Camera.GetComponent<DeathFade>();
            deathFade.enabled = true;
            deathFade.DisableEffect();
        }

        // Waits for the player to stop moving before performing inventory operations.
        // MUST be called before any operation that locks the inventory controller.
        public static async UniTask WaitUntilStationary(Player player)
        {
            await UniTask.WaitUntil(() => !player.MovementContext.CanWalk);
            await UniTask.Delay(200);
        }
    }
}
