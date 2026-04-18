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
using System.Linq;

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
    public override void Dispose()
    {
        _chains.Clear();
        base.Dispose();
    }

    private readonly Dictionary<int, UniTask> _chains = new();

    protected override bool ShouldNotifyAboutRejection => true;

    // protected override RateLimitConfig ServerRateLimit => new(
    //     enabled: true,
    //     refillPerSecond: 5,
    //     burst: 20,
    //     costPerPacket: 1,
    //     action: RateLimitAction.Reject,
    //     stateTtlSeconds: 60,
    //     rejectCooldownSeconds: 1.0);

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

    // we have to blindly accept our packet here otherwise ItemPlacement is not aware and tries to spawn multiple things in one grid
    // this entire packet needs to 
    protected override async void LocalPredictApproved(SpawnItemPacket packet)
    {
        // SpawnItem(packet, packet.Player);
        // we already spent money locally before requesting to begin with.
    }

    protected override bool EvaluatePacket(ref SpawnItemPacket packet, NetPeer peer, out string rejectionReason)
    {
        rejectionReason = null;

        if (!H.MainPlayerScore.CanBuy())
        {
            rejectionReason = "Buy time is over.";
            return false;
        }

        if (packet.item is VestItemClass or ArmorItemClass)
        {
            bool hasPlates = false;
            if (packet.item is ArmorItemClass armorItem)
            {
                if (armorItem.GetArmorPlates().Count() > 0)
                {
                    hasPlates = true;
                }
            }
            else if (packet.item is VestItemClass vestItem)
            {
                if (vestItem.IsTacRigArmored())
                {
                    if (vestItem.GetArmorPlates().Count() > 0)
                    {
                        hasPlates = true;

                    }
                }
            }

            if (hasPlates)
            {
                rejectionReason = "You can't buy a plate carrier with plates inside";
                return false;
            }
        }

        var placement = AU.GetItemPlacement(packet.item, packet.Player);

        if (placement.Kind == PlacementKind.None)
        {
            rejectionReason = "Server can't locate a viable location for a bought item";
            return false;
        }

        if (packet.placement.Address != placement.Address)
        {
            D.Log("Mismatching item placement, overriding");
            packet.placement = placement;
        }

        // forcing the item to 1 count (sometimes unstackable items get an insane value that makes no sense and breaks shit)
        if (packet.item.StackObjectsCount != 1)
            packet.item.StackObjectsCount = 1;

        return true;
    }

    protected override async void WhenApproved(SpawnItemPacket packet, NetPeer peer)
    {
        // if (packet.Player.IsYourPlayer) return;
        SpawnItem(packet, packet.Player);

        if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
        {
            H.GetPlayerScore(packet.Player.Id).SpendMoney(itemData.price);
        }
    }

    protected override void WhenRejected(SpawnItemPacket packet, NetPeer peer)
    {
        if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
        {
            H.MainPlayerScore.AddMoney(itemData.price);
        }
    }

    private async void SpawnItem(SpawnItemPacket packet, Player player)
    {
        if (!H.IsHeadless)
            await IU.LoadBundlesForItem(packet.item);
        await IU.WhenApprovedGiveItem(packet.item, player, packet.placement);
    }
}