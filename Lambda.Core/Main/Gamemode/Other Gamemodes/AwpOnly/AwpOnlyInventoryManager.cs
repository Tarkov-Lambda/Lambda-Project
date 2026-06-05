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

        var existingAWP = player.GetSlotItem(EquipmentSlot.FirstPrimaryWeapon) as Weapon;
        if (existingAWP == null) GiveAWP(player);
    }

    public override void HardReset(Player player)
    {
        base.HardReset(player);

        GiveAWP(player);
    }

    private void GiveAWP(Player player)
    {
        var AWP = GetFirstSniperRifleItem(player.Context);
        var AWPPlacement = AU.GetItemPlacement(AWP, player);

        if (AWPPlacement.Kind != PlacementKind.None)
        {
            AWPPlacement.Address.AddWithoutRestrictions(AWP);
        }
    }
}