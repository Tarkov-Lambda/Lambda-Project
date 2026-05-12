using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using MemoryPack;

namespace ifp.arena.bep.networking;

public class ItemPlacementFormatter : MemoryPackFormatter<ItemPlacement>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref ItemPlacement value)
    {
        writer.WriteObjectHeader(3);

        writer.WriteUnmanaged((int)value.Slot);
        writer.WriteUnmanaged((int)value.Kind);

        writer.WriteValue(value.Address);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref ItemPlacement value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = default;
            return;
        }

        var placementSlot = (EquipmentSlot)reader.ReadUnmanaged<int>();
        var placementKind = (PlacementKind)reader.ReadUnmanaged<int>();

        var address = reader.ReadValue<ItemAddress>();

        value = new ItemPlacement(placementKind, placementSlot, address);
    }
}