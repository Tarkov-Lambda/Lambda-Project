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
using PacketHandler;
using PacketHandler.RateLimiting;
using ifp.arena.shared.Models;

namespace ifp.arena.bep.networking;

public struct SpawnItemPacket : INetSerializable, IAuthoredPacket
{
    public Player Player { get; set; }
    public ItemPlacement placement;
    public Item item;

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPlayer(Player);
        writer.Put(placement);
        writer.PutItem(item);
    }

    public void Deserialize(NetDataReader reader)
    {
        Player = reader.GetPlayer();
        placement = reader.GetItemPlacement(Player);
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
            Player = H.MainPlayer,
            item = item,
            placement = placement
        };

        DispatchPacket(packet);
    }

    // we have to blindly accept our packet here otherwise ItemPlacement is not aware
    // and tries to spawn multiple things in one grid
    // otherwise we have to rewrite the logic to make the server give us spawn item packages effectivelly (gun + mags, 2 armor plates)
    protected override async void LocalPredictApproved(SpawnItemPacket packet)
    {
        SpawnItem(packet, packet.Player);
        // we already spent money locally before requesting to begin with.
    }

    protected override bool PacketValidation(ref SpawnItemPacket packet, NetPeer netPeer)
    {
        // var placement = AU.GetItemPlacement(packet.item, packet.Player);

        // if (packet.placement.Address == null)
        // {
        //     return false;
        // }

        // if (placement.Address != packet.placement.Address)
            // D.Log($"Placement mismatch for {packet.Player.Id}");
        // packet.placement = placement;
        return true;
    }

    protected override async void WhenApproved(SpawnItemPacket packet, NetPeer peer)
    {
        if (packet.Player.IsYourPlayer) return;
        SpawnItem(packet, packet.Player);

        if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
        {
            H.GetPlayerScore(packet.Player.Id).SpendMoney(itemData.price);
        }
    }

    private async void SpawnItem(SpawnItemPacket packet, Player player)
    {
        await IU.LoadBundlesForItem(packet.item);
        await IU.WhenApprovedGiveItem(packet.item, player, packet.placement);
    }

    public override void Dispose()
    {
        _chains.Clear();
        base.Dispose();
    }
}