using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core.Economy;
using PacketHandler;
using ifp.arena.shared.Models;
using Comfort.Common;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.Core.UI;

namespace ifp.arena.bep.networking;

public struct GiveMoneyPacket : INetSerializable, IAuthoredPacket
{
    public Player Player { get; set; }
    public Player TargetPlayer { get; set; }

    public string ItemBsgId;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<GiveMoneyPacket>(reader);
}

public class GiftMoneyPacketHandler : PacketHandler<GiveMoneyPacket>
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

    protected override void LocalPredictApproved(GiveMoneyPacket packet)
    {
        if (BuyMenuSelection.TryGetItemData(packet.ItemBsgId, out ShopItem itemData))
        {
            H.MainPlayerScore.SpendMoney(itemData.price);
        }
    }

    protected override bool ValidatePacket(ref GiveMoneyPacket packet, NetPeer peer, out string rejectionReason)
    {
        rejectionReason = null;

        AskForMoneyPacketHandler askForMoneyPacketHandler = Singleton<AskForMoneyPacketHandler>.Instance;

        if (askForMoneyPacketHandler.playerToItem.TryGetValue(packet.TargetPlayer, out string ItemBsgId))
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

    protected override async void WhenApproved(GiveMoneyPacket packet, NetPeer peer)
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

            AskForMoneyPacketHandler askForMoneyPacketHandler = Singleton<AskForMoneyPacketHandler>.Instance;
            askForMoneyPacketHandler.playerToItem.Remove(packet.TargetPlayer);
            EventBus.OnBuyAskCancelled?.Invoke(packet.Player);
        }
    }

    protected override void WhenRejected(GiveMoneyPacket packet, NetPeer peer)
    {
        if (BuyMenuSelection.TryGetItemData(packet.ItemBsgId, out ShopItem itemData))
        {
            H.MainPlayerScore.AddMoney(itemData.price);
        }

        base.WhenRejected(packet, peer);
    }
}