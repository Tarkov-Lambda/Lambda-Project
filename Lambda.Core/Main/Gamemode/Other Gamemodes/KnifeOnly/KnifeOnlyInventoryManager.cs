using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main;
using Lambda.Core.Main.Gamemode;

#pragma warning disable IDE0019

public class KnifeOnlyInventoryManager : InventoryManager
{
    public override void Replenish(Player player)
    {
        base.Replenish(player);

        var existingKnife = player.GetSlotItem(EquipmentSlot.Scabbard) as Weapon;
        if (existingKnife == null) GiveKnife(player);
    }

    public override void HardReset(Player player)
    {
        base.HardReset(player);

        GiveKnife(player);
    }

    private void GiveKnife(Player player)
    {
        IU.TryCreateItem(Hardcode.KNIFE, out Item knifeItem);
        var knifePlacement = AU.GetItemPlacement(knifeItem, player);

        if (knifePlacement.Kind != PlacementKind.None)
        {
            knifePlacement.Address.AddWithoutRestrictions(knifeItem);
        }
    }
}