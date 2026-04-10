using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using PacketHandler.RateLimiting;

namespace ifp.arena.bep.networking;

public struct PopPacket : INetSerializable, IAuthoredPacket
{
    public Player Player { get; set; }
    
    public Item item;
    public ItemAddress itemAddress;

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPlayer(Player);
        writer.PutItem(item);
        writer.Put(itemAddress);
    }

    public void Deserialize(NetDataReader reader)
    {
        Player = reader.GetPlayer();
        item = reader.GetItem();
        itemAddress = reader.GetItemAddress(Player);
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
            Player = H.MainPlayer,
            item = item,
            itemAddress = item.CurrentAddress
        };

        RequestSend(packet);
    }

    protected override void LocalPredictApproved(PopPacket packet)
    {
        IU.TryPopItemWithoutRestriction(packet.item, packet.itemAddress, packet.Player).Forget();
    }

    protected override async void WhenApproved(PopPacket packet, NetPeer peer)
    {
        if (packet.Player.IsYourPlayer) return;
        IU.TryPopItemWithoutRestriction(packet.item, packet.itemAddress, packet.Player).Forget();
    }
}