using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using ifp.arena.shared.Models;
using Lambda.Core.Main.Gamemode;

namespace Lambda.Core.Networking;

public struct AskForMoneyPacket : IPacket, IAuthoredPacket
{
    public Player Player { get; set; }
    public string ItemBsgId;
    // if true, the player asking for this item
    // if false, the player is saying "I don't want anything at all anymore"
    public bool IsRequesting;
}

public class AskForMoneyPacketWarden : LambdaPacketWarden<AskForMoneyPacket>
{
    public Dictionary<Player, string> playerToItem;

    public AskForMoneyPacketWarden()
    {
        playerToItem = [];

        EventBus.OnEnter += OnEnter;
    }

    public void OnEnter(MatchState matchState)
    {
        if (matchState == MatchState.Cleanup) playerToItem = [];
    }

    public override void Dispose()
    {
        EventBus.OnEnter -= OnEnter;

        playerToItem = [];
        base.Dispose();
    }

    public void Send(ShopItem shopItem) => Send(shopItem.bsgId);

    public void Send(Item item) => Send(item.TemplateId);

    public void Send(string itemBsgId)
    {
        if (H.Gamemode is not IGMTeam) return;

        var packet = new AskForMoneyPacket
        {
            Player = H.MainPlayer,
            ItemBsgId = itemBsgId,
            IsRequesting = playerToItem[H.MainPlayer] != itemBsgId ? true : false
        };

        DispatchPacket(packet);
    }

    protected override async void ApplyOptimistically(AskForMoneyPacket packet)
    {
        playerToItem[packet.Player] = packet.ItemBsgId;
    }

    protected override async void Apply(AskForMoneyPacket packet, int peerId)
    {
        if (packet.Player.IsYourPlayer) return;

        if (packet.IsRequesting)
        {
            playerToItem[packet.Player] = packet.ItemBsgId;
            EventBus.OnBuyAskStarted?.Invoke(packet.Player);
        }
        else
        {
            playerToItem.Remove(packet.Player);
            EventBus.OnBuyAskCancelled?.Invoke(packet.Player);
        }

    }
}