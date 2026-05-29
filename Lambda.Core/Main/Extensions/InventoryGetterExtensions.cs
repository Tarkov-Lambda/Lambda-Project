using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main.UI;

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
        if (compoundItem == null) return false;

        if (compoundItem.Slots.Any(slot => !string.IsNullOrEmpty(slot.Name) && slot.Name.EndsWith("_plate", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var armorHolder = compoundItem.GetItemComponent<ArmorHolderComponent>();
        if (armorHolder != null)
        {
            var hasAnyPlateSlots = armorHolder.ArmorSlots.Any(slot =>
                (!string.IsNullOrEmpty(slot.Name) && slot.Name.EndsWith("_plate", StringComparison.OrdinalIgnoreCase)) ||
                (slot.CachedSlotName != null && slot.CachedSlotName.EndsWith("_plate", StringComparison.OrdinalIgnoreCase))
            );
            return hasAnyPlateSlots;
        }

        return false;
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

        if (tacRig.CanFitPlates()) return true;

        return false;
    }

    public static IEnumerable<ArmorPlateItemClass> GetArmorPlates(this Player player)
    {
        CompoundItem plateCarrier = player.GetPlateCarrier();

        return plateCarrier?.GetArmorPlates();
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

        HashSet<string> validMagIds = new();

        foreach (var weapon in weapons)
        {
            var mag = weapon.GetMagTemplateForWeapon(player);
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

    public static MagazineItemClass GetMagTemplateForWeapon(this Weapon weapon, Player player)
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

    public static int CountAvailableArmorPlateSlots(this Player player)
    {
        var plateHolder = player.GetPlateCarrier();
        if (plateHolder == null) return 0;

        int count = 0;

        foreach (ArmorHolderComponent armorHolder in plateHolder.Components.OfType<ArmorHolderComponent>())
        {
            foreach (var slot in armorHolder.ArmorSlots)
            {
                if (slot.ContainedItem != null)
                    continue;

                bool isPlateSlot =
                    (!string.IsNullOrEmpty(slot.Name) && slot.Name.EndsWith("_plate", StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(slot.CachedSlotName) && slot.CachedSlotName.EndsWith("_plate", StringComparison.OrdinalIgnoreCase));

                if (isPlateSlot)
                    count++;
            }
        }

        foreach (var slot in plateHolder.Slots)
        {
            if (slot.ContainedItem != null)
                continue;

            if (!string.IsNullOrEmpty(slot.Name) &&
                slot.Name.EndsWith("_plate", StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }
}