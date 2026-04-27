using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Economy;
using PacketHandler;
using ifp.arena.shared.Models;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Fika.Core.Networking.Pooling;

namespace ifp.arena.bep.networking;

public struct BuyItemPacket : INetSerializable, IAuthoredPacket
{
    public Player Player { get; set; }
    public ItemPlacement placement;
    public Item item;

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPlayer(Player);
        writer.Put(placement);
        writer.PutItemCompressed(item);
    }

    public void Deserialize(NetDataReader reader)
    {
        Player = reader.GetPlayer();
        placement = reader.GetItemPlacement(Player);
        item = reader.GetItemCompressed();
    }
}

public class BuyItemPacketHandler : PacketHandler<BuyItemPacket>
{
    protected override bool ShouldNotifyAboutRejection => true;

    protected override bool ShouldProcessInstantly => false;

    public void Send(Item item, ItemPlacement placement)
    {
        var packet = new BuyItemPacket
        {
            Player = H.MainPlayer,
            item = item, // this item is only a template, the server clones it before application
            placement = placement,
        };

        DispatchPacket(packet);
    }

    protected override bool ValidatePacket(BuyItemPacket packet, NetPeer peer, out string rejectionReason)
    {
        if (H.Session.matchState != MatchState.Cleanup)
        {
            if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
            {
                var playerScore = packet.Player.GetScore();
                if (playerScore.Money < itemData.price)
                {
                    rejectionReason = "You don't have enough money";
                    return false;
                }
            }
        }

        return base.ValidatePacket(packet, peer, out rejectionReason);
    }

    // SERVER: Wait for the host's representation of the player's inventory to settle, then mutate and broadcast.
    protected override void ProcessApprovedPacket(ref BuyItemPacket packet, NetPeer peer)
    {
        int playerId = packet.Player.Id;
        var localPacket = packet;

        PlayerInventoryTimeGate.Enqueue(playerId, () =>
        {
            MutateApprovedPacket(ref localPacket, peer);

            if (localPacket.placement.Kind == PlacementKind.None)
            {
                SendRejection(ref localPacket, peer, "Can't find placement for your item");
                return;
            }

            H.FikaNet.SendData(ref localPacket, deliveryMethod, true);
            ApplyInternal(localPacket, peer);
        });
    }

    // CLIENT: Wait for the observing client's representation of the player's inventory to settle, then apply locally.
    protected override void WhenClientReceivesPacket(BuyItemPacket packet, NetPeer peer)
    {
        int playerId = packet.Player.Id;
        var localPacket = packet;

        PlayerInventoryTimeGate.Enqueue(playerId, () => base.WhenClientReceivesPacket(localPacket, peer));
    }

    protected override void MutateApprovedPacket(ref BuyItemPacket packet, NetPeer peer)
    {
        packet.item = packet.item.CloneItem();
        packet.placement = AU.GetItemPlacement(packet.item, packet.Player);
    }

    protected override void Apply(BuyItemPacket packet, NetPeer peer)
    {
        // Money is only authentically spent here. Rejecting early doesn't cost anything.
        if (H.Session.matchState != MatchState.Cleanup)
        {
            if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
            {
                H.GetPlayerScore(packet.Player.Id).SpendMoney(itemData.price);
            }
        }

        IU.WhenApprovedGiveItem(packet.item, packet.Player, packet.placement);
    }

    protected override void WhenRejected(BuyItemPacket packet, NetPeer peer)
    {
        if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
        {
            H.GetPlayerScore(packet.Player.Id).AddMoney(itemData.price);
        }
    }
}

public static class PlayerInventoryTimeGate
{
    private static readonly ConcurrentDictionary<int, PlayerQueue> _playerQueues = new();

    public static void Enqueue(int playerId, Action action)
    {
        var queue = _playerQueues.GetOrAdd(playerId, id => new PlayerQueue(id));
        queue.Enqueue(action);
    }

    public static void ClearAll()
    {
        _playerQueues.Clear();
    }

    private class PlayerQueue
    {
        private readonly int _playerId;
        private readonly ConcurrentQueue<Action> _queue = new();
        private int _isProcessing = 0; // 0/1 false/true

        public PlayerQueue(int playerId)
        {
            _playerId = playerId;
        }

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
                    Player player = H.GetPlayer(_playerId);

                    // Native EFT/Fika inventory operations take varying lengths of time to clear active events
                    if (player != null && player.InventoryController is TraderControllerClass traderController)
                    {
                        float timeout = 3.0f; // Absolute failsafe to prevent infinite softlock
                        while (traderController.HasActiveEvents && timeout > 0)
                        {
                            await UniTask.Delay(5);
                            timeout -= 0.005f;
                        }
                    }

                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        D.LogError($"[PlayerInventoryTimeGate] Error processing packet: {ex}");
                    }
                }
            }
            finally
            {
                // mark as stopped
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