using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT.InventoryLogic;
using Lambda.Core.Main;
using Lambda.Core.Main.UI;
using Lambda.Core.GameTypes;
using Lambda.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lambda.Core.Main.Gamemode;

public static class Purchasing
{
    public static async void BuyItem(ShopItem request)
    {
        // delai
        Item item = PresetItemsCache.Instance.GetPresetItem(request.bsgId);
        H.MainPlayerScore.SpendMoney(request.price);
        bool isSuccessful = await IU.ClientRequestBuyItem(item);
        if (isSuccessful)
        {
            H.EFTGUISounds.PlayUISound(EFT.UI.EUISoundType.TradeOperationComplete);
            EventBus.OnItemBuy?.Invoke(request);
        }
        else H.MainPlayerScore.AddMoney(request.price);
    }
}
