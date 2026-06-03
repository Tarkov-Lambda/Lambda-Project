using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Networking;

namespace Lambda.Core.Main;

public static class ReplenishmentUtilities
{
    public static void Replenish(Player player, bool shouldReloadGun = true)
    {
        foreach (var slot in player.Equipment.AllSlots)
        {
            foreach (var item in slot.Items)
            {
                item.Repair();

                if (item is Weapon weapon)
                {
                    if (FU.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
                    {
                        if (shouldReloadGun)
                        {
                            ReplenishGun(weapon, ammo);
                        }

                        ReplenishMagazines(weapon, player, ammo);
                    }
                }
            }
        }
    }

    public static void SetupWeapon(Weapon weapon, Player player)
    {
        weapon.SwitchFullAutoIfNeeded();

        if (FU.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
        {
            ReplenishGun(weapon, ammo);

            ReplenishMagazines(weapon, player, ammo);
        }
    }

    public static void ReplenishMagazinesImmediate(Weapon weapon, Player player)
    {
        var vest = player.GetSlotItem(EquipmentSlot.TacticalVest) as CompoundItem;
        var pockets = player.GetPlayerPockets();
        if (vest == null && pockets == null) return;

        string weaponMagTemplate = weapon.GetMagTemplateForWeapon(player)?.TemplateId;
        if (weaponMagTemplate == null) return;

        if (!FU.TryGetGunAmmo(weapon, out AmmoItemClass ammo)) return;

        var existingMags = player.GetMatchingMags(weaponMagTemplate, vest).ToList();

        foreach (var mag in existingMags)
        {
            ReplenishMagazine(mag, ammo);
        }

        int missing = 3 - existingMags.Count;
        if (missing <= 0) return;

        for (int i = 0; i < missing; i++)
        {
            if (!IU.TryCreateItem(weaponMagTemplate, out Item newItem)) continue;
            if (newItem is not MagazineItemClass newMag) continue;

            ReplenishMagazine(newMag, ammo);

            var placement = AU.GetItemPlacement(newMag, player);
            if (placement.Kind != PlacementKind.None)
            {
                placement.Address.AddWithoutRestrictions(newMag);
            }
        }
    }

    public static void SwitchFullAutoIfNeeded(this Weapon weapon)
    {
        var firemode = weapon.Components.Find(c => c is FireModeComponent) as FireModeComponent;

        if (firemode != null && firemode.AvailableEFireModes.Contains(Weapon.EFireMode.fullauto))
        {
            firemode.FireMode = Weapon.EFireMode.fullauto;
        }
    }

    public static void SetupWeaponLocally(Weapon weapon, Player player)
    {
        weapon.SwitchFullAutoIfNeeded();

        if (FU.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
        {
            ReplenishGun(weapon, ammo);

            ReplenishMagazinesImmediate(weapon, player);
        }
    }

    // refactor this
    public static void ReplenishMagazines(Weapon weapon, Player player, AmmoItemClass ammo)
    {
        if (player.GetSlotItem(EquipmentSlot.TacticalVest) is not CompoundItem vest) return;

        string weaponMagTemplate = weapon.GetMagTemplateForWeapon(player)?.TemplateId;
        if (weaponMagTemplate == null)
        {
            D.LogError($"Can't find {weapon.LocalizedName()}'s mag");
            return;
        }

        // Collect all matching mags from vest grids and pockets in one pass.
        var mags = player.GetMatchingMags(weaponMagTemplate, vest);

        if (ammo == null)
        {
            FU.TryGetGunAmmo(weapon, out AmmoItemClass newAmmo);
            ammo = newAmmo;
        }

        foreach (var mag in mags)
        {
            ReplenishMagazine(mag, ammo);
        }

        if (H.IsClient) return; // server gives new mags

        int missing = 3 - mags.Count();
        if (missing <= 0)
            return;

        for (int i = 0; i < missing; i++)
        {
            if (!IU.TryCreateItem(weaponMagTemplate, out Item newItem))
                continue;

            if (newItem is not MagazineItemClass newMag)
                continue;

            ReplenishMagazine(newMag, ammo);

            var placement = AU.GetItemPlacement(newMag, player);

            if (AU.GetItemPlacement(newMag, player).Kind == PlacementKind.None)
            {
                D.NotifyLong("Can't find space for a mag");
                continue;
            }

            Singleton<BuyItemPacketWarden>.Instance.Send(newItem, placement, player);
        }
    }

    public static void ReplenishGun(Weapon weapon, AmmoItemClass ammo)
    {
        var magazine = weapon.GetCurrentMagazine();

        if (magazine != null)
        {
            ReplenishMagazine(magazine, ammo);
        }

        FillSlotsWithAmmo(weapon.Chambers, ammo);
    }

    public static void ReplenishMagazine(MagazineItemClass magazine, AmmoItemClass ammo)
    {
        // Handle cylinder magazines
        if (magazine is CylinderMagazineItemClass cylinder)
        {
            FillSlotsWithAmmo(cylinder.Camoras, ammo);
            return;
        }

        if (magazine.Cartridges != null)
        {
            var topAmmoItem = magazine.Cartridges.Items.LastOrDefault();

            if (topAmmoItem != null)
            {
                topAmmoItem.StackObjectsCount = Math.Min(topAmmoItem.Template.StackMaxSize, magazine.MaxCount);
            }
            else if (IU.TryCreateItem(ammo.TemplateId, out Item newItem))
            {
                newItem.StackObjectsCount = magazine.MaxCount;
                magazine.Cartridges.Add(newItem, simulate: false);
            }
        }
    }

    private static void FillSlotsWithAmmo(IEnumerable<Slot> slots, AmmoItemClass ammo)
    {
        foreach (var slot in slots)
        {
            if (slot.ContainedItem == null && IU.TryCreateItem(ammo.TemplateId, out Item newItem))
            {
                slot.AddWithoutRestrictions(newItem);
            }
        }
    }

    public static void ReplaceMagIfNeeded(this Weapon weapon)
    {

    }

    private static void Repair(this Item item)
    {
        if (item is Weapon weapon)
        {
            weapon.Repairable.MaxDurability = 100f;
            weapon.Repairable.Durability = 100f;
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
                        armor.Repairable.MaxDurability = armor.Repairable.TemplateDurability;
                        armor.Repairable.Durability = armor.Repairable.TemplateDurability;
                    }
                }
            }
        }
    }
}