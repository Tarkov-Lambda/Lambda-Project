using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;

namespace ifp.arena.bep.networking;

public static class LambdaSerializationExtension
{
    public static void Put(this NetDataWriter writer, ItemPlacement placement)
    {
        writer.Put((int)placement.Slot);
        writer.Put((int)placement.Kind);

        writer.Put(placement.Address);
    }

    public static ItemPlacement GetItemPlacement(this NetDataReader reader, Player player)
    {
        var placementSlot = (EquipmentSlot)reader.GetInt();
        var placementKind = (PlacementKind)reader.GetInt();
        ItemAddress address = reader.GetItemAddress(player);

        return new ItemPlacement(placementKind, placementSlot, address);
    }
}