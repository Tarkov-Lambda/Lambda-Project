using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using PacketHandler.RateLimiting;
using ifp.arena.bep.Core;
using System;
using Comfort.Common;
using Fika.Core.Main.Players;

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

public class ForceRemoveItemPacketHandler : PacketHandler<PopPacket>
{
    // protected override RateLimitConfig ServerRateLimit => RateLimitPresets.StrictInteraction;

    public void Send(Item item)
    {
        var packet = new PopPacket
        {
            Player = H.MainPlayer,
            item = item,
            itemAddress = item.CurrentAddress,
        };

        DispatchPacket(packet);
    }

    protected override async void Apply(PopPacket packet, NetPeer peer)
    {
        try
        {
            // sussy
            if (!packet.Player.IsYourPlayer && packet.Player is ObservedPlayer obsPlayer)
            {
                obsPlayer.OperationCallbacks.Clear();
            }

            packet.Player.TryPopItemWithoutRestriction(packet.item, packet.itemAddress);
        }
        catch (Exception e)
        {
            D.Log($"An error has occured in {GetType().Name}");
            D.Log(e.Message);
            D.Log(e.StackTrace);
        }
    }
}