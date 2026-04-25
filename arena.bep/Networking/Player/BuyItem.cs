using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Economy;
using PacketHandler;
using ifp.arena.shared.Models;
using Cysharp.Threading.Tasks.Triggers;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace ifp.arena.bep.networking;

public struct BuyItemPacket : INetSerializable, IAuthoredPacket
{
    public Player Player { get; set; }
    public ItemPlacement placement;
    public Item Item;

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPlayer(Player);
        writer.Put(placement);
        writer.PutItem(Item);
    }

    public void Deserialize(NetDataReader reader)
    {
        Player = reader.GetPlayer();
        placement = reader.GetItemPlacement(Player);
        Item = reader.GetItem();
    }
}

public class BuyItemPacketHandler : PacketHandler<BuyItemPacket>
{
    protected override bool ShouldNotifyAboutRejection => true;

    public void Send(Item item, ItemPlacement placement)
    {
        var packet = new BuyItemPacket
        {
            Player = H.MainPlayer,
            Item = item,
            placement = placement,
        };

        if (H.IsServer)
        {
            packet.Item = packet.Item.CloneItem();
        }

        DispatchPacket(packet);
    }

    protected override bool ValidatePacket(BuyItemPacket packet, NetPeer peer, out string rejectionReason)
    {
        if (H.Session.matchState != MatchState.Cleanup)
        {
            if (BuyMenuSelection.TryGetItemData(packet.Item.TemplateId, out ShopItem itemData))
            {
                var playerScore = packet.Player.GetScore();
                if (playerScore.Money < itemData.price)
                {
                    rejectionReason = "You don't have enough money";
                    return false;
                }
            }
        }

        rejectionReason = null;
        return true;
    }

    // I don't like this but I'm genuinely tired and I need to create a queue
    protected override void AfterServerApprovesPacket(ref BuyItemPacket packet, NetPeer peer)
    {
        packet.Item = packet.Item.CloneItem();

        BuyItemPacket queuedPacket = packet;
        NetPeer queuedPeer = peer;
        int playerId = packet.Player.Id;

        PlayerInventoryTimeGate.Enqueue(playerId, () =>
        {
            var placement = AU.GetItemPlacement(queuedPacket.Item, queuedPacket.Player);
            if (queuedPacket.placement.Address != placement.Address)
                queuedPacket.placement = placement;

            base.AfterServerApprovesPacket(ref queuedPacket, queuedPeer);
        });
    }

    protected override void WhenApproved(BuyItemPacket packet, NetPeer peer)
    {
        if (H.Session.matchState != MatchState.Cleanup)
        {
            if (BuyMenuSelection.TryGetItemData(packet.Item.TemplateId, out ShopItem itemData))
            {
                H.GetPlayerScore(packet.Player.Id).SpendMoney(itemData.price);
            }
        }

        IU.WhenApprovedGiveItem(packet.Item, packet.Player, packet.placement);
    }

    protected override void WhenRejected(BuyItemPacket packet, NetPeer peer)
    {
        if (BuyMenuSelection.TryGetItemData(packet.Item.TemplateId, out ShopItem itemData))
        {
            H.MainPlayerScore.AddMoney(itemData.price);
        }
    }
}

public static class PlayerInventoryTimeGate
{
    private static readonly ConcurrentDictionary<int, PlayerQueue> _playerQueues = new();

    public static void Enqueue(int playerId, Action action)
    {
        var queue = _playerQueues.GetOrAdd(playerId, _ => new PlayerQueue());
        queue.Enqueue(action);
    }

    public static void ClearAll()
    {
        _playerQueues.Clear();
    }

    private class PlayerQueue
    {
        private readonly ConcurrentQueue<Action> _queue = new();
        private int _isProcessing = 0; // 0/1 false/true

        public void Enqueue(Action action)
        {
            _queue.Enqueue(action);

            // if the loop isn't running - start it
            if (Interlocked.Exchange(ref _isProcessing, 1) == 0)
            {
                ProcessQueueAsync().Forget();
            }
        }

        private async UniTaskVoid ProcessQueueAsync()
        {
            try
            {
                while (_queue.TryDequeue(out var action))
                {
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        D.LogError($"[PlayerInventoryTimeGate] Error processing packet: {ex}");
                    }

                    await UniTask.Delay(25);
                }
            }
            finally
            {
                // narkd as stopped
                Interlocked.Exchange(ref _isProcessing, 0);

                // if a packet was enqueued exactly as we were stopping, restart the loop.
                if (!_queue.IsEmpty && Interlocked.Exchange(ref _isProcessing, 1) == 0)
                {
                    ProcessQueueAsync().Forget();
                }
            }
        }
    }
}