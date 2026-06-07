using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main.Gamemode;

public class SNDInventoryManager : KnifeOnlyInventoryManager
{
    public override void Replenish(Player player)
    {
        base.Replenish(player);

        var pistol = player.GetSlotItem(EquipmentSlot.Holster) as Weapon;
        pistol ??= GiveDefaultPistol(player);

        pistol.MalfState.ChangeStateSilent(Weapon.EMalfunctionState.None);
    }

    public override void HardReset(Player player)
    {
        base.HardReset(player);

        GiveDefaultPistol(player);
    }

    private PistolItemClass GiveDefaultPistol(Player player)
    {
        PistolItemClass defaultPistol = GetDefaultPistol(player.Context).CloneItem();
        var pistolPlacement = AU.GetItemPlacement(defaultPistol, player);
        pistolPlacement.Address.AddWithoutRestrictions(defaultPistol);
        RU.SetupWeaponLocally(defaultPistol, player);

        return defaultPistol;
    }
} 