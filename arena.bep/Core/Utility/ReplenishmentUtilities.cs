using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core;
using ifp.arena.bep.networking;

namespace ifp.arena.bep.Core;

public static class ReplenishmentUtilities
{
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
                    if (FU.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
                    {
                        if (shouldReloadGun)
                        {
                            ReplenishGun(weapon, ammo);
                        }

                        ReplenishVestMagazines(weapon, ammo, player).Forget();
                    }
                }
            }
        }
    }

    public static void SetupWeaponAfterEquip(Weapon weapon, Player player)
    {
        if (FU.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
        {
            ReplenishGun(weapon, ammo);

            // Only the local player's machine should create and broadcast vest magazines.
            if (player.IsYourPlayer) ReplenishVestMagazines(weapon, ammo, player).Forget();
        }

        var firemode = weapon.Components.Find(c => c is FireModeComponent) as FireModeComponent;
        if (firemode != null && firemode.AvailableEFireModes.Contains(Weapon.EFireMode.fullauto))
        {
            firemode.FireMode = Weapon.EFireMode.fullauto;
        }
    }

    // Local only, sends spawn item packets
    public static async UniTask ReplenishVestMagazines(Weapon weapon, AmmoItemClass ammo, Player player)
    {
        await UniTask.Delay(25);
        Slot vest = player.Equipment.GetSlot(EquipmentSlot.TacticalVest);

        if (vest?.ContainedItem is not CompoundItem vestCompound)
            return;

        string weaponMagTemplate = weapon.GetCurrentMagazine()?.TemplateId;
        if (weaponMagTemplate == null)
        {
            D.LogError($"Can't find {weapon.LocalizedName()}'s mag");
            return;
        }

        // Collect all matching mags from vest grids and pockets in one pass.
        var mags = PU.GetMatchingMags(player, vestCompound, weaponMagTemplate);

        foreach (var mag in mags)
        {
            ReplenishMagazine(mag, ammo);
        }

        int missing = 3 - mags.Count;
        if (missing <= 0)
            return;

        for (int i = 0; i < missing; i++)
        {
            if (!IU.TryCreateItem(weaponMagTemplate, out Item newItem))
                continue;

            if (newItem is not MagazineItemClass newMag)
                continue;

            ReplenishMagazine(newMag, ammo);

            if (AU.GetItemPlacement(newMag, player).Kind == PlacementKind.None)
            {
                D.NotifyLong("Can't find space for a mag");
                continue;
            }

            await IU.ClientRequestGiveItem(newMag);
            await UniTask.Delay(25);
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
}