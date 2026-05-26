using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main;
using Lambda.Core.Main.Economy;
using Lambda.Shared.Models;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using PacketWarden.RateLimiting;
using MemoryPack;
using System.Diagnostics;

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

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(0.15);

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

            if (packet.item is HeadwearItemClass)
            {
                if (packet.Player.CountAvailableArmorPlateSlots() > 0)
                {
                    rejectionReason = $"You must buy armor first";
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
        else if (packet.item is ArmorPlateItemClass)
        {
            int availablePlateSlots = packet.Player.CountAvailableArmorPlateSlots();
            for (int i = 0; i < availablePlateSlots - 1; i++)
            {
                var anotherPlatePacket = new BuyItemPacket
                {
                    Player = packet.Player,
                    item = packet.item,
                };
                DispatchPacket(ref anotherPlatePacket); // auto fill slots
            }
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

// Whilst buying items in raid is one of the most important parts of this mod
// I do not have the expertise nor the time to learn Tarkov/Fika to correctly find a segment to hook into
// because of this, I've made this very gnarly system that errors every now and then, but is solvable with equipment resynchronization
public static class PlayerInventoryTimeGate
{
    private static readonly ConcurrentDictionary<int, PlayerQueue> _playerQueues = new();

    private static CancellationTokenSource _cts = new();

    private const float TimeoutSeconds = 3.0f;

    public static void Enqueue(int playerId, Action action)
    {
        if (action == null) return;

        var queue = _playerQueues.GetOrAdd(playerId, id => new PlayerQueue(id));
        queue.Enqueue(action);
    }

    public static void ClearAll()
    {
        _playerQueues.Clear();

        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    private sealed class PlayerQueue
    {
        private readonly int _playerId;
        private readonly ConcurrentQueue<Action> _queue = new();

        // 0/1 false/true
        private int _isProcessing;

        public PlayerQueue(int playerId) => _playerId = playerId;

        public void Enqueue(Action action)
        {
            _queue.Enqueue(action);

            // start processing if not already running
            if (Interlocked.Exchange(ref _isProcessing, 1) == 0)
                ProcessQueueAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid ProcessQueueAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _queue.TryDequeue(out var action))
                {
                    Player player = null;
                    try { player = H.GetPlayer(_playerId); }
                    catch { /* ignored */ }

                    await WaitForInventoryToSettle(player, ct);

                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception ex)
                    {
                        D.LogError($"[PlayerInventoryTimeGate] Error processing action for player {_playerId}: {ex}");
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);

                // race-safe restart if something was enqueued after loop ended
                if (!_queue.IsEmpty && !ct.IsCancellationRequested && Interlocked.Exchange(ref _isProcessing, 1) == 0)
                    ProcessQueueAsync(ct).Forget();
            }
        }

        private static async UniTask WaitForInventoryToSettle(Player player, CancellationToken ct)
        {
            long endTicks = Stopwatch.GetTimestamp() + (long)(TimeoutSeconds * Stopwatch.Frequency);

            if (!player.InventoryController.HasActiveEvents)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                return;
            }

            while (!ct.IsCancellationRequested && Stopwatch.GetTimestamp() < endTicks)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

                if (!player.InventoryController.HasActiveEvents)
                    break;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
    }
}