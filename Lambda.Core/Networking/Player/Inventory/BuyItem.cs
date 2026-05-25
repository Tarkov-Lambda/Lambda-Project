using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main;
using Lambda.Core.Main.Economy;
using Lambda.Shared.Models;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using System.Linq;
using PacketWarden.RateLimiting;
using MemoryPack;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct BuyItemPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    

    [MemoryPackAllowSerialize]
    public ItemPlacement placement;
    
    [MemoryPackAllowSerialize]
    public Item item;
}

public class BuyItemPacketWarden : LambdaPacketWarden<BuyItemPacket>
{
    protected override bool ShouldNotifyAboutRejection => true;

    protected override bool ShouldProcessInstantly => false;

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(0.1);

    public void Send(Item item, ItemPlacement placement, Player player)
    {
        var packet = new BuyItemPacket
        {
            Player = player,
            item = item, // this item is only a template, the server clones it before application
            // placement = placement,
        };

        DispatchPacket(ref packet);
    }

    protected override bool ValidatePacket(BuyItemPacket packet, int peerId, out string rejectionReason)
    {
        if (H.Session.matchState != MatchState.Cleanup)
        {
            if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
            {
                var playerScore = packet.Player.GetContext();

                if (playerScore.Money < itemData.price)
                {
                    rejectionReason = "You don't have enough money";
                    return false;
                }

                if (playerScore.HasReachedLimit(itemData))
                {
                    rejectionReason = $"Limit reached for this round ({itemData.maxBuy})";
                    return false;
                }
            }
        }

        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }


    // SERVER: Wait for the host's representation of the player's inventory to settle, then mutate and broadcast.
    protected override void ProcessApprovedPacket(ref BuyItemPacket packet, int peerId)
    {
        int playerId = packet.Player.Id;
        var localPacket = packet;

        PlayerInventoryTimeGate.Enqueue(playerId, () =>
        {
            MutateApprovedPacket(ref localPacket, peerId); // THIS CAN REJECT IF PLACEMENT IS NOT FOUND
            if (localPacket.placement.Kind == PlacementKind.None)
            {
                SendRejection(ref localPacket, peerId, "Can't find placement for your item");
                return;
            }

            PacketWardenUtils.Network.SendData(ref localPacket, DeliveryType, true);
            ApplyInternal(localPacket, peerId);
        });
    }

    // CLIENT: Wait for the observing client's representation of the player's inventory to settle, then apply locally.
    // I don't know if this will play nicely, but it's "good enough" for now
    protected override void WhenClientReceivesPacket(BuyItemPacket packet, int peerId)
    {
        int playerId = packet.Player.Id;
        var localPacket = packet;

        PlayerInventoryTimeGate.Enqueue(playerId, () => base.WhenClientReceivesPacket(localPacket, peerId));
    }

    protected override void MutateApprovedPacket(ref BuyItemPacket packet, int peerId)
    {
        packet.item = packet.item.CloneItem();
        packet.placement = AU.GetItemPlacement(packet.item, packet.Player);

        if (packet.placement.Kind == PlacementKind.None)
        {
            SendRejection(ref packet, peerId, "Can't find placement for your item");
            return;
        }

        // this logic needs to be relocated
        if (packet.item is Weapon weapon)
        {
            IU.DowngradeMagIfNeeded(weapon);
            RU.SetupWeapon(weapon, packet.Player);
        }
        else if (packet.item is HeadwearItemClass headwear)
        {
            IU.AttachNightVisionIfNeeded(headwear);
        }
    }

    protected override void Apply(BuyItemPacket packet, int peerId)
    {
        if (BuyMenuSelection.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
        {
            H.GetPlayerScore(packet.Player.Id).SpendMoney(itemData.price);

            packet.Player.GetContext().AddItemQuantity(itemData);
        }

        if (packet.placement.Kind == PlacementKind.EquipmentSlot && packet.item is Weapon)
        {
            packet.Player.HandsController?.FastForwardCurrentState();
        }

        packet.Player.PlaceItem(packet.item, packet.placement);
    }

    protected override void WhenRejected(BuyItemPacket packet, int peerId)
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

    private class PlayerQueue(int playerId)
    {
        private readonly int _playerId = playerId;
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
                    Player player = H.GetPlayer(_playerId);

                    float timeout = 3.0f;
                    while (player.InventoryController.HasActiveEvents && timeout > 0f)
                    {
                        await UniTask.DelayFrame(1);
                        timeout -= Time.deltaTime;
                    }

                    await UniTask.DelayFrame(1);

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
                Interlocked.Exchange(ref _isProcessing, 0);

                if (!_queue.IsEmpty && Interlocked.Exchange(ref _isProcessing, 1) == 0)
                {
                    ProcessQueueAsync().Forget();
                }
            }
        }
    }
}