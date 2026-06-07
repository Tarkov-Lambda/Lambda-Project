using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main;
using Lambda.Core.Main.Gamemode;

#pragma warning disable IDE0019

public class AwpOnlyInventoryManager : BaseInventoryManager
{
    public override void Replenish(Player player)
    {
        base.Replenish(player);

        var AWP = player.GetSlotItem(EquipmentSlot.FirstPrimaryWeapon) as Weapon;
        AWP ??= GiveAWP(player);
        RU.SetupWeaponLocally(AWP, player);
    }

    public override void HardReset(Player player)
    {
        base.HardReset(player);

        var AWP = GiveAWP(player);
        RU.SetupWeaponLocally(AWP, player);
    }

    private SniperRifleItemClass GiveAWP(Player player)
    {
        var AWP = GetFirstSniperRifleItem(player.Context);
        var AWPPlacement = AU.GetItemPlacement(AWP, player);

        if (AWPPlacement.Kind != PlacementKind.None)
        {
            AWPPlacement.Address.AddWithoutRestrictions(AWP);
        }

        return AWP;
    }
}