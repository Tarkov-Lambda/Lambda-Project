using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main;
using Lambda.Core.Main.Gamemode;

#pragma warning disable IDE0019

public class SNDInventoryManager : BaseInventoryManager
{
    public override void Replenish(Player player)
    {
        base.Replenish(player);

        var pistol = player.GetSlotItem(EquipmentSlot.Holster) as Weapon;
        if (pistol == null) pistol = GiveDefaultPistol(player);

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