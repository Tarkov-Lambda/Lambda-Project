using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.UI;
using ifp.arena.bep.networking;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using UnityEngine;

namespace ifp.arena.bep.Core
{
    public static class PlayerUtils
    {
        // Applies a permanent painkiller at the start of the raid
        public static void ApplyPainkiller()
        {
            var healthController = H.MainPlayer.ActiveHealthController;
            Type painkillerType = AccessTools.TypeByName("EFT.HealthSystem.ActiveHealthController+PainKiller");

            bool hasPainkiller = healthController.GetAllEffects()
                .Any(effect => effect.GetType() == painkillerType && effect.BodyPart == EBodyPart.Head);

            if (!hasPainkiller)
            {
                healthController.DoPainKiller();
            }
        }

        public static List<Weapon> GetAllWeapons(Player player)
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

        // FIKA DOES NOT SYNC DURABILITY REPAIRS
        // Though I think it does sync equipment changes from client automatically (player still has to manually invoke RaiseEvents)
        public static void Replenish(Player player, bool shouldReloadGun = true)
        {
            foreach (var slot in player.Equipment.AllSlots)
            {
                foreach (var item in slot.Items)
                {
                    RepairItem(item);

                    if (item is Weapon weapon)
                    {

                        if (PresetUtils.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
                        {
                            if (shouldReloadGun)
                            {
                                ReplenishGun(weapon, ammo);
                            }

                            ReplenishVestMagazines(weapon, ammo, player);
                        }

                    }


                }
            }
        }

        public static void ReplenishVestMagazines(Weapon weapon, AmmoItemClass ammo, Player player)
        {
            Slot vest = player.Equipment.GetSlot(EquipmentSlot.TacticalVest);

            if (vest?.ContainedItem is not CompoundItem vestCompound)
                return;

            string weaponMagTemplate = weapon.GetCurrentMagazine()?.TemplateId;
            if (weaponMagTemplate == null)
                return;

            List<MagazineItemClass> mags = new();

            foreach (var grid in vestCompound.Grids)
            {
                foreach (var item in grid.Items)
                {
                    if (item is MagazineItemClass mag && mag.TemplateId == weaponMagTemplate)
                        mags.Add(mag);
                }
            }

            foreach (var mag in mags)
            {
                ReplenishMagazine(mag, ammo);
            }

            int missing = 3 - mags.Count;
            if (missing <= 0)
                return;

            for (int i = 0; i < missing; i++)
            {
                if (!ItemsUtils.TryCreateItem(weaponMagTemplate, out Item newItem))
                    continue;

                if (newItem is not MagazineItemClass newMag)
                    continue;

                ReplenishMagazine(newMag, ammo);

                // Check there's actually room before broadcasting, so we don't send
                // a SpawnItemPacket for a mag that has nowhere to land.
                bool hasRoom = false;
                foreach (var grid in vestCompound.Grids)
                {
                    if (grid.TryFindLocationForItem(newMag, out _))
                    {
                        hasRoom = true;
                        break;
                    }
                }

                if (!hasRoom)
                    continue;

                Singleton<SpawnItemPacketHandler>.Instance.Send(newMag);
            }
        }

        public static void ReplenishGun(Weapon weapon, AmmoItemClass ammo)
        {
            var magazine = weapon.GetCurrentMagazine();

            if (magazine != null)
            {
                ReplenishMagazine(magazine, ammo);
            }

            foreach (var chamber in weapon.Chambers)
            {
                if (chamber.ContainedItem == null && ItemsUtils.TryCreateItem(ammo.TemplateId, out Item newItem))
                {
                    chamber.AddWithoutRestrictions(newItem);
                }
            }
        }

        public static void ReplenishMagazine(MagazineItemClass magazine, AmmoItemClass ammo)
        {
            // Handle cylinder magazines
            if (magazine is CylinderMagazineItemClass cylinder)
            {
                foreach (var camora in cylinder.Camoras)
                {
                    if (camora.ContainedItem == null && ItemsUtils.TryCreateItem(ammo.TemplateId, out Item newItem))
                    {
                        camora.AddWithoutRestrictions(newItem);
                    }
                }
                return;
            }

            if (magazine.Cartridges != null)
            {
                var topAmmoItem = magazine.Cartridges.Items.LastOrDefault();

                if (topAmmoItem != null)
                {
                    topAmmoItem.StackObjectsCount = Math.Min(topAmmoItem.Template.StackMaxSize, magazine.MaxCount);
                }
                else if (ItemsUtils.TryCreateItem(ammo.TemplateId, out Item newItem))
                {
                    newItem.StackObjectsCount = magazine.MaxCount;
                    magazine.Cartridges.Add(newItem, simulate: false);
                }
            }
        }

        private static void RepairItem(Item item)
        {
            if (item is Weapon weapon)
            {
                weapon.Repairable.Durability = 100;
                weapon.MalfState.LastShotOverheat = 0f;
            }
            else if (item is CompoundItem compoundItem)
            {
                foreach (var slot in compoundItem.AllSlots)
                {
                    foreach (var childItem in slot.Items)
                    {
                        if (childItem is ArmoredEquipmentItemClass armor)
                        {
                            armor.Repairable.Durability = armor.Repairable.MaxDurability;
                        }
                    }
                }
            }
        }

        // This is really stupid and the amount of replenish shit im doing is really bad
        public static async Task FixMe()
        {
            var health = H.MainPlayer.ActiveHealthController;
            Replenish(H.MainPlayer, true);

            health.ChangeHydration(100f);
            health.ChangeEnergy(100f);
            health.RestoreFullHealth();

            await Task.Delay(500);

            foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
            {
                health.RemoveNegativeEffects(bodyPart);
            }

            // Replenish(H.MainPlayer);
            health.RestoreFullHealth();
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
    }

}