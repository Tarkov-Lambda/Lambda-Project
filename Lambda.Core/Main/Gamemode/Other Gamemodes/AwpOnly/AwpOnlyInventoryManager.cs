using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main;
using Lambda.Core.Main.Gamemode;

#pragma warning disable IDE0019

public class AwpOnlyInventoryManager : KnifeOnlyInventoryManager
{
    public override void Replenish(Player player)
    {
        base.Replenish(player);

        var ExistingRifle = player.GetSlotItem(EquipmentSlot.FirstPrimaryWeapon) as Weapon;
        ExistingRifle?.CurrentAddress.RemoveWithoutRestrictions(ExistingRifle);

        List<Item> residualMags = new();

        AddRange(ref residualMags, player.GetNonMatchingMags());
        foreach (var itemToRemove in residualMags)
            itemToRemove.CurrentAddress.RemoveWithoutRestrictions(itemToRemove);

        Weapon NewRifle = GiveCurrentRoundRifle(player) as Weapon;
        RU.SetupWeaponLocally(NewRifle, player);
    }

    public override void HardReset(Player player)
    {
        base.HardReset(player);

        Weapon NewRifle = GiveCurrentRoundRifle(player) as Weapon;
        RU.SetupWeaponLocally(NewRifle, player);
    }

    private Item GiveCurrentRoundRifle(Player player)
    {
        var AwpOnlyGamemode = H.Gamemode as AwpOnlyGamemode;

        string SelectedWeaponBsgId = AwpOnlyGamemode.RoundGunType switch
        {
            RoundGunType.TRG => Hardcode.TRG,
            RoundGunType.MK18 => Hardcode.MK18,
            RoundGunType.AK50 => Hardcode.AK50,
            _ => null
        };

        var NewRifle = GetBuySelectionItem(player.Context, SelectedWeaponBsgId);
        if (NewRifle is not null)
        {
            var AWPPlacement = AU.GetItemPlacement(NewRifle, player);

            if (AWPPlacement.Kind != PlacementKind.None)
                AWPPlacement.Address.AddWithoutRestrictions(NewRifle);
        }

        return NewRifle;
    }
}