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

    public static void ForceAddAndRaiseForServerPlayer(this ItemAddress address, Item item, Player player)
    {
        address.AddWithoutRestrictions(item);
        if (!H.IsHeadless && player.IsYourPlayer && H.IsServer)
        {
            address.RaiseAddEvent(item, CommandStatus.Begin, player.InventoryController);
            address.RaiseAddEvent(item, CommandStatus.Succeed, player.InventoryController);
        }
    }

    public static void ForceRemoveAndRaiseForServerPlayer(this ItemAddress address, Item item, Player player)
    {
        address.RemoveWithoutRestrictions(item);
        if (!H.IsHeadless && player.IsYourPlayer && H.IsServer)
        {
            address.RaiseRemoveEvent(item, CommandStatus.Begin, player.InventoryController);
            address.RaiseRemoveEvent(item, CommandStatus.Succeed, player.InventoryController);
        }
    }
}