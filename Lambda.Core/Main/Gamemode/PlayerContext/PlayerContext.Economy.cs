using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main.Economy;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using Lambda.Shared.Models;
using PacketWarden.TimeSync;

namespace Lambda.Core.Main;

public partial class PlayerContext
{
    public void AddMoney(int amount)
    {
        context.Economy.Money += amount;

        context.Economy.Money = Math.Clamp(Money, 0, EconomyConstants.MAX_MONEY);

        if (H.IsHeadless) return;
        if (player == H.MainPlayer)
            EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
    }

    public void SpendMoney(int amount)
    {
        if (H.Gamemode is IGMBuyable)
        {
            context.Economy.Money -= amount;
            if (Money < 0) context.Economy.Money = 0;

            if (H.IsHeadless) return;
            EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
        }
    }

    public void SetMoney(int newMoney)
    {
        context.Economy.Money = newMoney;
    }

    public bool HasReachedLimit(ShopItem shopItem)
    {
        if (shopItem.maxBuy <= 0) return false;

        if (ItemQuantityBoughtInRound.TryGetValue(shopItem, out int count))
        {
            return count >= shopItem.maxBuy;
        }
        return false;
    }

    public void AddItemQuantity(ShopItem shopItem)
    {
        if (ItemQuantityBoughtInRound.ContainsKey(shopItem))
        {
            ItemQuantityBoughtInRound[shopItem]++;
        }
        else
        {
            ItemQuantityBoughtInRound[shopItem] = 1;
        }
    }


    public bool CanBuy()
    {
        if (H.Gamemode is IGMBuyable buyable)
        {
            if (H.Session.matchState is MatchState.RoundPrepare) return true;
            if (buyable.CanBuyInActivePhase)
            {
                if (H.Session.matchState is MatchState.RoundAction)
                {
                    return H.Arena.StateTimer >= H.Arena.PhaseDurationSeconds - buyable.TimeInActivePhaseToBuy;
                }
            }
        }

        return false;
    }
}