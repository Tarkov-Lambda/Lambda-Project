using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Economy;
using PacketHandler;
using ifp.arena.shared.Models;
using System.Linq;
using Fika.Core.Main.Players;
using Fika.Core.Main.ObservedClasses;
using HarmonyLib;
using System.Reflection;
using System.Collections.Concurrent;

namespace ifp.arena.bep.networking;

public struct BuyItemPacket : INetSerializable, IAuthoredPacket
{
    public Player Player { get; set; }
    public ItemPlacement placement;
    public Item item;
    public MongoID inventoryMongoID;

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPlayer(Player);
        writer.Put(placement);
        writer.PutItem(item);
        writer.PutMongoID(inventoryMongoID);
    }

    public void Deserialize(NetDataReader reader)
    {
        Player = reader.GetPlayer();
        placement = reader.GetItemPlacement(Player);
        item = reader.GetItem();
        inventoryMongoID = reader.GetMongoID();
    }
}

public class BuyItemPacketHandler : PacketHandler<BuyItemPacket>
{
    public override void Dispose()
    {
        // Complete all channels to stop workers
        foreach (var writer in _playerQueues.Values)
        {
            writer.TryComplete();
        }
        _playerQueues.Clear();
        base.Dispose();
    }

    private readonly ConcurrentDictionary<Player, ChannelWriter<BuyItemPacket>> _playerQueues = new();

    private MethodInfo _setNewIdMethod = AccessTools.Method(typeof(ObservedInventoryController), "SetNewID");

    protected override bool ShouldNotifyAboutRejection => true;

    protected override bool ShouldBroadcastApprovalsToAll(BuyItemPacket packet) => false;

    public void Send(Item item, ItemPlacement placement)
    {
        var packet = new BuyItemPacket
        {
            Player = H.MainPlayer,
            item = item,
            placement = placement,
            inventoryMongoID = H.MainPlayer.InventoryController.CurrentId,
        };

        DispatchPacket(packet);
    }

    protected override bool EvaluatePacket(ref BuyItemPacket packet, NetPeer peer, out string rejectionReason)
    {
        rejectionReason = null;

        // if the gamemode is not IBuyable, allow anyone to buy anything
        // if (H.Session.matchState != MatchState.Cleanup && H.ActiveRules is IGMBuyable)
        // {
        //     if (!H.GetPlayerScore(packet.Player).CanBuy())
        //     {
        //         rejectionReason = "Buy time is over.";
        //         return false;
        //     }
        // }

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

        // if (packet.placement.Address != placement.Address)
        // {
        //     D.Log("Mismatching item placement, overriding");
        //     packet.placement = placement;
        // }

        // forcing the item to 1 count (sometimes unstackable items get an insane value that makes no sense and breaks shit)
        if (packet.item.StackObjectsCount != 1)
            packet.item.StackObjectsCount = 1;

        return true;
    }


    protected override void WhenApproved(BuyItemPacket packet, NetPeer peer)
    {
        var writer = _playerQueues.GetOrAdd(packet.Player, player =>
        {
            var channel = Channel.CreateSingleConsumerUnbounded<BuyItemPacket>();
            RunPlayerWorker(channel.Reader, player).Forget();
            return channel.Writer;
        });

        writer.TryWrite(packet);
    }

    private async UniTaskVoid RunPlayerWorker(ChannelReader<BuyItemPacket> reader, Player player)
    {
        try
        {
            // ReadAllAsync returns an IUniTaskAsyncEnumerable, which supports sequential await
            await foreach (var packet in reader.ReadAllAsync())
            {
                await ProcessPurchaseSequentially(packet);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            D.Log($"An error has occured in {player.Profile.Nickname} buying worker");
            D.Log(e.Message);
            D.Log(e.StackTrace);
        }
        finally
        {
            _playerQueues.TryRemove(player, out _);
        }
    }

    private async UniTask ProcessPurchaseSequentially(BuyItemPacket packet)
    {
        if (H.IsServer)
        {
            packet.placement = AU.GetItemPlacement(packet.item, packet.Player);
            // now that we have evaluated and are approving the item, clone it using the player's InventoryController's Mongo ID's ID Generator
            packet.item = packet.item.CloneItem(packet.Player.InventoryController);
            // log the observed player's mongo id
            packet.inventoryMongoID = packet.Player.InventoryController.CurrentId;
        }


        if (packet.Player.IsYourPlayer)
        {
            var currentId = packet.Player.InventoryController.CurrentId;
            if (packet.inventoryMongoID.Counter > currentId.Counter || packet.inventoryMongoID.TimeStamp != currentId.TimeStamp)
            {
                packet.Player.InventoryController.MongoID_0 = packet.inventoryMongoID;
            }
        }
        else if (packet.Player is ObservedPlayer obsPlayer && obsPlayer.InventoryController is ObservedInventoryController obsController)
        {
            _setNewIdMethod?.Invoke(obsController, new object[] { packet.inventoryMongoID });
        }

        await IU.LoadBundlesForItem(packet.item);
        IU.WhenApprovedGiveItem(packet.item, packet.Player, packet.placement);

        // after incrementing mongo id and placing the item, broadcast the approval manually here (this is fucking horrendous but I'm forced)
        if (H.IsServer)
        {
            H.FikaNet.SendData(ref packet, this.deliveryMethod, true);
        }

        if (H.Session.matchState != MatchState.Cleanup)
        {
            if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
            {
                H.GetPlayerScore(packet.Player.Id).SpendMoney(itemData.price);
            }
        }
    }

    protected override void WhenRejected(BuyItemPacket packet, NetPeer peer)
    {
        if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
        {
            H.MainPlayerScore.AddMoney(itemData.price);
        }
    }
}