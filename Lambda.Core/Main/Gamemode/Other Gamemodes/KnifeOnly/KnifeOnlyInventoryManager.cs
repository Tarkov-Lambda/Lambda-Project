using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main;
using Lambda.Core.Main.Gamemode;

#pragma warning disable IDE0019

public class KnifeOnlyInventoryManager : BaseInventoryManager
{
    public override void Replenish(Player player)
    {
        base.Replenish(player);

        var existingKnife = player.GetSlotItem(EquipmentSlot.Scabbard) as KnifeItemClass;
        _ = existingKnife ?? GiveKnife(player);
    }

    public override void HardReset(Player player)
    {
        base.HardReset(player);

        GiveKnife(player);
    }
}