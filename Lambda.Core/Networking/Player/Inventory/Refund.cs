using EFT;
using System;
using PacketWarden.RateLimiting;
using MemoryPack;
using Comfort.Common;
using System.Collections.Generic;
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
        if (!TryFindLatestTransaction(player, templateId, out Guid transactionId))
        {
            D.Notify("Can't find this transaction");
            return;
        }

        var packet = new RefundItemPacket
        {
            Player = player,
            ID = transactionId
        };

        DispatchPacket(ref packet);
    }

    private static bool TryFindLatestTransaction(Player player, string templateId, out Guid transactionId)
    {
        transactionId = Guid.Empty;

        foreach (var transaction in Singleton<BuyItemPacketWarden>.Instance.RoundTransactions)
        {
            List<BuyItemPacket> packets = transaction.Value;

            if (packets.Count > 0 && packets[0].Player == player && packets[0].item.TemplateId == templateId)
                transactionId = transaction.Key;
        }

        return transactionId != Guid.Empty;
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

        if (allRoundTransactions.TryGetValue(packet.ID, out var transactions) && transactions.Count > 0)
        {
            allRoundTransactions.Remove(packet.ID);

            if (BuyMenuSelection.TryGetItemData(transactions[0].item.TemplateId, out ShopItem itemData))
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