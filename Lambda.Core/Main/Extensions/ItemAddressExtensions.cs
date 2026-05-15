using EFT;
using EFT.InventoryLogic;

public static class ItemAddressExtensions
{
    public static void RaiseForceAdd(this ItemAddress address, Item item, Player player)
    {
        address.RaiseAddEvent(item, CommandStatus.Begin, player.InventoryController);
        address.RaiseAddEvent(item, CommandStatus.Succeed, player.InventoryController);
    }

    public static void RaiseForceRemove(this ItemAddress address, Item item, Player player)
    {
        address.RaiseRemoveEvent(item, CommandStatus.Begin, player.InventoryController);
        address.RaiseRemoveEvent(item, CommandStatus.Succeed, player.InventoryController);
    }
}