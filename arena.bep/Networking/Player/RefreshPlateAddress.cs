using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;

namespace ifp.arena.bep.networking;

public struct PopPacket : INetSerializable, IAuthoredPacket
{
    public Player player { get; set; }
    
    public Item item;
    public ItemAddress itemAddress;

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPlayer(player);
        writer.PutItem(item);
        writer.Put(itemAddress);
    }

    public void Deserialize(NetDataReader reader)
    {
        player = reader.GetPlayer();
        item = reader.GetItem();
        itemAddress = reader.GetItemAddress(player);
    }
}

public class RemoveItemPacketHandler : PacketHandler<PopPacket>
{

    protected override RateLimitConfig ServerRateLimit => new(
        enabled: true,
        refillPerSecond: 5,
        burst: 20,
        costPerPacket: 1,
        action: RateLimitAction.Reject,
        stateTtlSeconds: 60,
        rejectCooldownSeconds: 1.0);

    public void Send(Item item)
    {
        var packet = new PopPacket
        {
            player = H.MainPlayer,
            item = item,
            itemAddress = item.CurrentAddress
        };

        RequestSend(packet);
    }

    protected override void LocalPredictApproved(PopPacket packet)
    {
        IU.TryPopItemWithoutRestriction(packet.item, packet.itemAddress, packet.player).Forget();
    }

    protected override async void WhenApproved(PopPacket packet, NetPeer peer)
    {
        if (packet.player.IsYourPlayer) return;
        IU.TryPopItemWithoutRestriction(packet.item, packet.itemAddress, packet.player).Forget();
    }
}