using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.Core.UI;
using ifp.arena.shared.Models;
using UnityEngine;

namespace ifp.arena.bep.Core;

public static class InventoryGetterExtensions
{
    public static Item GetSlotItem(this Player player, EquipmentSlot slotType) => player.Equipment.GetSlot(slotType).ContainedItem;

    public static SearchableItemItemClass GetPlayerPockets(this Player player) => player.Equipment.GetSlot(EquipmentSlot.Pockets).ContainedItem as SearchableItemItemClass;

    public static VestItemClass GetTacRig(this Player player) => player.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as VestItemClass;

    public static IEnumerable<T> GetVestAndPocketGridItems<T>(this Player player, CompoundItem vest = null) where T : Item
    {
        vest ??= player.GetSlotItem(EquipmentSlot.TacticalVest) as CompoundItem;

        var pockets = GetPlayerPockets(player);

        if (vest?.Grids != null)
        {
            foreach (var grid in vest.Grids)
            {
                if (grid?.Items == null) continue;

                foreach (var item in grid.Items)
                {
                    if (item is T typed)
                        yield return typed;
                }
            }
        }

        if (pockets?.Grids != null)
        {
            foreach (var grid in pockets.Grids)
            {
                if (grid?.Items == null) continue;

                foreach (var item in grid.Items)
                {
                    if (item is T typed)
                        yield return typed;
                }
            }
        }
    }
    public static IEnumerable<MagazineItemClass> GetMatchingMags(this Player player, string magTemplateId, CompoundItem vest = null)
    {
        return GetVestAndPocketGridItems<MagazineItemClass>(player, vest).Where(m => m.TemplateId == magTemplateId);
    }

    public static List<Weapon> GetAllWeapons(this Player player)
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

    public static bool CanFitPlates(this CompoundItem compoundItem)
    {
        var armorHolder = compoundItem.GetItemComponent<ArmorHolderComponent>();

        if (armorHolder == null)
            return false;

        var hasAnyPlateSlots = armorHolder.ArmorSlots.Any(slot => slot.CachedSlotName != null && slot.CachedSlotName.EndsWith("_plate", StringComparison.OrdinalIgnoreCase));
        return hasAnyPlateSlots;
    }

    public static CompoundItem GetPlateCarrier(this Player player)
    {
        if (player.GetSlotItem(EquipmentSlot.TacticalVest) is VestItemClass tacRig)
        {
            if (tacRig.IsTacRigArmored())
            {
                return tacRig;
            }
        }

        if (player.GetSlotItem(EquipmentSlot.ArmorVest) is ArmorItemClass armorVest)
            return armorVest;

        return null;
    }

    public static bool IsTacRigArmored(this VestItemClass tacRig)
    {
        var tacRigTemplate = tacRig?.Template as VestTemplateClass;
        if (tacRigTemplate.BlocksArmorVest) return true;
        return false;
    }

    public static IEnumerable<ArmorPlateItemClass> GetArmorPlates(this Player player)
    {
        CompoundItem plateCarrier = player.GetPlateCarrier();

        return plateCarrier.GetArmorPlates();
    }

    public static IEnumerable<ArmorPlateItemClass> GetArmorPlates(this CompoundItem plateCarrier)
    {
        if (plateCarrier.TryGetItemComponent<ArmorHolderComponent>(out var armorHolder))
        {
            return armorHolder.MoveAbleArmorPlates;
        }

        return [];
    }

    public static IEnumerable<MagazineItemClass> GetNonMatchingMags(this Player player)
    {
        var weapons = player.GetAllWeapons();

        HashSet<string> validMagIds = new HashSet<string>();

        foreach (var weapon in weapons)
        {
            var mag = GetMagTemplateForWeapon(weapon);
            if (mag != null)
            {
                validMagIds.Add(mag.TemplateId);
            }
        }

        var mags = player.GetVestAndPocketGridItems<MagazineItemClass>();

        foreach (var mag in mags)
        {
            if (!validMagIds.Contains(mag.TemplateId))
            {
                yield return mag;
            }
        }
    }

    public static MagazineItemClass GetMagTemplateForWeapon(this Weapon weapon)
    {
        MagazineItemClass currentWeaponMag = weapon.GetCurrentMagazine();
        if (currentWeaponMag != null)
        {
            return currentWeaponMag;
        }


        if (PresetItemsCache.Instantiated)
        {
            Weapon presetWeapon = PresetItemsCache.Instance.GetPresetItem(weapon.TemplateId) as Weapon;
            MagazineItemClass presetWeaponMag = presetWeapon.GetCurrentMagazine();
            if (presetWeaponMag != null)
            {
                return presetWeaponMag;
            }
        }

        // thanks bsg for putting two different g36 mag holder variations
        // whoever is reading this fuck you
        WeaponBuildClass defaultPresetWeaponBuild = FU.Presets.FirstOrDefault(b => b.FromPreset && b.Item.TemplateId == weapon.TemplateId);
        Weapon defaultPresetWeapon = defaultPresetWeaponBuild.Item as Weapon;

        MagazineItemClass defaultWeaponMag = defaultPresetWeapon.GetCurrentMagazine();
        if (defaultWeaponMag != null)
        {
            return defaultWeaponMag;
        }

        D.LogError($"Can't match a mag for {weapon.LocalizedName()}");
        return null;
    }
}