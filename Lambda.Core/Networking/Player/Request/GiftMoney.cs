using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main.Economy;
using Lambda.Shared.Models;
using Comfort.Common;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Main.UI;
using MemoryPack;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct GiveMoneyPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }
    
    [MemoryPackAllowSerialize]
    public Player TargetPlayer { get; set; }

    public string ItemBsgId;
}

public class GiftMoneyPacketWarden : LambdaPacketWarden<GiveMoneyPacket>
{
    protected override bool ShouldNotifyAboutRejection => true;

    public void Send(Player targetPlayer, ShopItem shopItem) => Send(targetPlayer, shopItem.bsgId);

    public void Send(Player targetPlayer, Item item) => Send(targetPlayer, item.TemplateId);

    public void Send(Player targetPlayer, string itemBsgId)
    {
        var packet = new GiveMoneyPacket
        {
            Player = H.MainPlayer,
            TargetPlayer = targetPlayer,
            ItemBsgId = itemBsgId
        };

        DispatchPacket(packet);
    }

    protected override void ApplyOptimistically(GiveMoneyPacket packet)
    {
        if (BuyMenuSelection.TryGetItemData(packet.ItemBsgId, out ShopItem itemData))
        {
            H.MainPlayerScore.SpendMoney(itemData.price);
        }
    }

    protected override bool ValidatePacket(GiveMoneyPacket packet, int peerId, out string rejectionReason)
    {
        rejectionReason = null;

        AskForMoneyPacketWarden askForMoneyPacketWarden = Singleton<AskForMoneyPacketWarden>.Instance;

        if (askForMoneyPacketWarden.playerToItem.TryGetValue(packet.TargetPlayer, out string ItemBsgId))
        {
            if (ItemBsgId != packet.ItemBsgId)
            {
                rejectionReason = $"{packet.TargetPlayer} is not requesting this item.";
                return false;
            }

            return true;
        }

        rejectionReason = $"{packet.TargetPlayer} is not requesting an item at the moment.";
        return false;
    }

    protected override async void Apply(GiveMoneyPacket packet, int peerId)
    {
        if (BuyMenuSelection.TryGetItemData(packet.ItemBsgId, out ShopItem itemData))
        {
            H.GetPlayerScore(packet.Player.Id).SpendMoney(itemData.price);
            H.GetPlayerScore(packet.TargetPlayer.Id).AddMoney(itemData.price);

            if (packet.TargetPlayer.IsYourPlayer)
            {
                var presetItem = PresetItemsCache.Instance.GetPresetItem(packet.ItemBsgId);
                D.Notify($"{packet.Player} bought {presetItem.LocalizedName()} for you");
                Purchasing.BuyItem(itemData);
            }

            AskForMoneyPacketWarden askForMoneyPacketWarden = Singleton<AskForMoneyPacketWarden>.Instance;
            askForMoneyPacketWarden.playerToItem.Remove(packet.TargetPlayer);
            EventBus.OnBuyAskCancelled?.Invoke(packet.Player);
        }
    }

    protected override void WhenRejected(GiveMoneyPacket packet, int peerId)
    {
        if (BuyMenuSelection.TryGetItemData(packet.ItemBsgId, out ShopItem itemData))
        {
            H.MainPlayerScore.AddMoney(itemData.price);
        }

        base.WhenRejected(packet, peerId);
    }
}