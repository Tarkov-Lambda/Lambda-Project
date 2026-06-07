using EFT;
using System;
using PacketWarden.RateLimiting;
using MemoryPack;
using Comfort.Common;
using System.Linq;
using PacketWarden.TimeSync;
using Lambda.Core.Main.Economy;
using Lambda.Shared.Models;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct RefundItemPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public Guid ID;
}

// all the inventory add logic should just be rewritten from scratch
public class RefundItemPacketWarden : LambdaPacketWarden<RefundItemPacket>
{
    protected override bool ShouldNotifyAboutRejection => true;
    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(0.15);

    public void Send(Player player, string templateId)
    {
        var packet = new RefundItemPacket
        {
            Player = player
        };

        var allRoundTransactions = Singleton<BuyItemPacketWarden>.Instance.RoundTransactions;
        var foundTransaction = allRoundTransactions.FirstOrDefault(transaction => transaction.Value?[0].item.TemplateId == templateId);

        if (foundTransaction.Value == null)
        {
            D.Notify("Can't find this transaction");
            return;
        }

        packet.ID = foundTransaction.Key;

        DispatchPacket(ref packet);
    }

    protected override bool ValidatePacket(RefundItemPacket packet, int peerId, out string rejectionReason)
    {
        var allRoundTransactions = Singleton<BuyItemPacketWarden>.Instance.RoundTransactions;
        if (allRoundTransactions.TryGetValue(packet.ID, out var transactions))
        {
            foreach (var transaction in transactions)
            {
                if (transaction.Player != packet.Player)
                {
                    rejectionReason = "Action unauthorized";
                    return false;
                }

                double ageInSeconds = NetworkTime.ServerNowSeconds - transaction.Timestamp;

                bool hasEnoughTimePassed = ageInSeconds >= 2;
                if (!hasEnoughTimePassed)
                {
                    rejectionReason = "You must wait before refunding this transaction";
                    return false;
                }

                var item = transaction.Player.FindItemById(transaction.item.Id);
                if (item.Failed)
                {
                    rejectionReason = "One or more items linked to the transaction can not be found";
                    return false;
                }
            }
        }
        else
        {
            rejectionReason = "Can't find requested transaction";
            return false;
        }

        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }

    protected override void Apply(RefundItemPacket packet, int peerId)
    {
        var allRoundTransactions = Singleton<BuyItemPacketWarden>.Instance.RoundTransactions;

        if (allRoundTransactions.TryGetValue(packet.ID, out var transactions))
        {
            var firstTransaction = transactions.FirstOrDefault();

            if (BuyMenuSelection.TryGetItemData(firstTransaction.item.TemplateId, out ShopItem itemData))
            {
                packet.Player.Context.AddMoney(itemData.price);
            }

            foreach (var transaction in transactions)
            {
                var itemFindResult = transaction.Player.FindItemById(transaction.item.Id);

                if (itemFindResult.Failed)
                {
                    continue;
                }

                var item = itemFindResult.Value;
                var cachedAddress = item.CurrentAddress;

                cachedAddress.RemoveWithoutRestrictions(item);
                cachedAddress.RaiseForceRemove(item, transaction.Player);
            }
        }
    }
}