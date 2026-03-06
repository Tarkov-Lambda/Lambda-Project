using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using ifp.arena.bep.Patches.Tarkov;
using UnityEngine;

namespace ifp.arena.bep.Core
{
    public static class PlayerUtils
    {
        // Applies a permanent painkiller at the start of the raid
        public static void ApplyPainkiller()
        {
            if (!H.isInRaid()) return;

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

        public static void RegisterBullet(Weapon weapon)
        {
            MagazineItemClass magazine = weapon.GetCurrentMagazine();
            AmmoItemClass ammo = magazine.GetBulletAtPosition(0);

            Patch_FirearmController_InitiateShot.AmmoRegistry[weapon] = new MagAndAmmo
            {
                magazine = magazine,
                ammo = ammo
            };
        }

        public static void RegisterAllBullets()
        {
            foreach (Player player in H.GetAllPlayers())
            {
                foreach (Weapon weapon in GetAllWeapons(player))
                {
                    H.Notify(weapon.ShortName.ToString());
                    RegisterBullet(weapon);
                }
            }
        }

        // Replenish all equipped weapons and armor
        public static void Replenish(Player player, bool shouldReloadGun = true)
        {
            Slot tacticalVest = player.Equipment.GetSlot(EquipmentSlot.TacticalVest);

            foreach (var slot in player.Equipment.AllSlots)
            {
                foreach (var item in slot.Items)
                {
                    RepairItem(item);

                    if (item is Weapon weapon)
                    {
                        if (shouldReloadGun)
                        {
                            ReplenishGun(weapon, H.AmmoRegistry[weapon].ammo);
                        }

                        ReplenishVestMagazines(tacticalVest, weapon);
                    }
                }
            }
        }

        public static void ReplenishVestMagazines(Slot vest, Weapon weapon)
        {
            if (vest != null && vest.ContainedItem is CompoundItem vestCompound)
            {
                foreach (var grid in vestCompound.Grids)
                {
                    if (grid.Items.FirstOrDefault() is MagazineItemClass magazine)
                    {
                        ReplenishMagazine(magazine, H.AmmoRegistry[weapon].ammo);
                    }
                }
            }
        }

        public static void ReplenishGun(Weapon weapon, AmmoItemClass ammo)
        {
            var magazine = weapon.GetCurrentMagazine();

            if (magazine != null)
            {
                ReplenishMagazine(magazine, ammo);
                return;
            }

            foreach (var chamber in weapon.Chambers)
            {
                if (chamber.ContainedItem == null && TryCreateItem(ammo.TemplateId, out Item newItem))
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
                    if (camora.ContainedItem == null && TryCreateItem(ammo.TemplateId, out Item newItem))
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
                else if (TryCreateItem(ammo.TemplateId, out Item newItem))
                {
                    newItem.StackObjectsCount = magazine.MaxCount;
                    magazine.Cartridges.Add(newItem, simulate: false);
                }
            }
        }

        private static bool TryCreateItem(string templateId, out Item newItem)
        {
            newItem = null;

            if (!Singleton<ItemFactoryClass>.Instantiated || !Singleton<ItemFactoryClass>.Instance.ItemTemplates.ContainsKey(templateId))
                return false;

            newItem = Singleton<ItemFactoryClass>.Instance.CreateItem(MongoID.Generate(), templateId, itemDiff: null);
            return newItem != null;
        }

        private static void RepairItem(Item item)
        {
            if (item is Weapon weapon)
            {
                weapon.Repairable.Durability = 100;
            }

            if (item is CompoundItem compoundItem)
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
            Replenish(H.MainPlayer);

            health.ChangeHydration(100f);
            health.ChangeEnergy(100f);
            health.RestoreFullHealth();

            await Task.Delay(500);

            foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
            {
                health.RemoveNegativeEffects(bodyPart);
            }

            Replenish(H.MainPlayer);
            health.RestoreFullHealth();
        }

        public static async Task CloseEyes(bool playAudio = true, bool openAfter = true, int delay = 2000)
        {
            DeathFade deathFade = CameraClass.Instance.Camera.GetComponent<DeathFade>();
            deathFade.enabled = true;

            await Task.Delay(250);
            deathFade.EnableEffect();

            if (playAudio)
            {
                var resourceRequest = Resources.LoadAsync<UISoundsWrapper>("Audio/UISoundsWrapper");
                var soundsWrapper = (UISoundsWrapper)resourceRequest.asset;
                var uIClip = soundsWrapper.GetUIClip(EUISoundType.PlayerIsDead);

                Singleton<GUISounds>.Instance.PlaySound(uIClip, false, true);
                Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.PlayerIsDead);
            }

            if (openAfter)
            {
                await Task.Delay(delay);
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