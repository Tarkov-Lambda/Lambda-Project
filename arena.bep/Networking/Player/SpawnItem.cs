using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;
using ifp.arena.shared.Models;

namespace ifp.arena.bep.networking;

public struct SpawnItemPacket : INetSerializable, IAuthoredPacket
{
    public Player player { get; set; }
    public ItemPlacement placement;
    public Item item;

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPlayer(player);
        writer.Put(placement);
        writer.PutItem(item);
    }

    public void Deserialize(NetDataReader reader)
    {
        player = reader.GetPlayer();
        placement = reader.GetItemPlacement(player);
        item = reader.GetItem();
    }
}

public class SpawnItemPacketHandler : PacketHandler<SpawnItemPacket>
{
    private readonly Dictionary<int, UniTask> _chains = new();

    protected override RateLimitConfig ServerRateLimit => new(
        enabled: true,
        refillPerSecond: 5,
        burst: 20,
        costPerPacket: 1,
        action: RateLimitAction.Reject,
        stateTtlSeconds: 60,
        rejectCooldownSeconds: 1.0);

    public void Send(Item item, ItemPlacement placement)
    {
        var packet = new SpawnItemPacket
        {
            player = H.MainPlayer,
            item = item,
            placement = placement
        };

        RequestSend(packet);
    }

    // we have to blindly accept our packet here otherwise ItemPlacement is not aware
    // and tries to spawn multiple things in one grid
    // otherwise we have to rewrite the logic to make the server give us spawn item packages effectivelly (gun + mags, 2 armor plates)
    protected override async void LocalPredictApproved(SpawnItemPacket packet)
    {
        SpawnItem(packet, packet.player);
        // we already spent money locally before requesting to begin with.
    }

    protected override async void WhenApproved(SpawnItemPacket packet, NetPeer peer)
    {
        if (packet.player.IsYourPlayer) return;
        SpawnItem(packet, packet.player);

        if (BuyMenu.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
        {
            H.GetPlayerScore(packet.player.Id).SpendMoney(itemData.price);
        }
    }

    private async void SpawnItem(SpawnItemPacket packet, Player player)
    {
        await IU.LoadBundlesForItem(packet.item);
        await IU.WhenApprovedGiveItem(packet.item, player, packet.placement);
    }

    public new void Dispose()
    {
        _chains.Clear();
        base.Dispose();
    }
}