using System.Collections.Generic;
using System.Threading.Tasks;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using UnityEngine;

namespace ifp.arena.bep.Core
{
    public static class PlayerUtils
    {
        public static SearchableItemItemClass GetPlayerPockets(Player player) => player.Equipment.GetSlot(EquipmentSlot.Pockets).ContainedItem as SearchableItemItemClass;
        public static Item GetPlayerSlotItem(Player player, EquipmentSlot slotType) => player.Equipment.GetSlot(slotType).ContainedItem;

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

                Singleton<GUISounds>.Instance.PlaySound(uIClip, false, true);
                Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.PlayerIsDead);
            }

            if (openAfter)
            {
                await Task.Delay(openDelay);
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
