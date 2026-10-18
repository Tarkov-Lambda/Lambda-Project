using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main.Gamemode;
using Lambda.Shared.Models;
using MemoryPack;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct AskForMoneyPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public string ItemBsgId;

    // if true, the player asking for this item
    // if false, the player is saying "I don't want anything at all anymore"
    public bool IsRequesting;
}

public class AskForMoneyPacketWarden : LambdaPacketWarden<AskForMoneyPacket>
{
    public readonly Dictionary<Player, string> playerToItem = [];

    public AskForMoneyPacketWarden()
    {
        EventBus.OnEnter += OnEnter;
    }

    public void OnEnter(MatchState matchState)
    {
        if (matchState == MatchState.Cleanup)
            playerToItem.Clear();
    }

    public override void Dispose()
    {
        EventBus.OnEnter -= OnEnter;

        playerToItem.Clear();
        base.Dispose();
    }

    public void Send(ShopItem shopItem) => Send(shopItem.bsgId);

    public void Send(Item item) => Send(item.TemplateId);

    public void Send(string itemBsgId)
    {
        if (H.Gamemode is not IGMTeam)
            return;

        // asking for the same item again cancels the request
        bool isRequesting =
            !playerToItem.TryGetValue(H.MainPlayer, out string requestedItem)
            || requestedItem != itemBsgId;

        var packet = new AskForMoneyPacket
        {
            Player = H.MainPlayer,
            ItemBsgId = itemBsgId,
            IsRequesting = isRequesting,
        };

        DispatchPacket(ref packet);
    }

    protected override void ApplyOptimistically(AskForMoneyPacket packet)
    {
        UpdateRequest(packet);
    }

    protected override void Apply(AskForMoneyPacket packet, int peerId)
    {
        if (packet.Player.IsYourPlayer)
            return;

        UpdateRequest(packet);

        if (packet.IsRequesting)
            EventBus.OnBuyAskStarted?.Invoke(packet.Player);
        else
            EventBus.OnBuyAskCancelled?.Invoke(packet.Player);
    }

    private void UpdateRequest(AskForMoneyPacket packet)
    {
        if (packet.IsRequesting)
            playerToItem[packet.Player] = packet.ItemBsgId;
        else
            playerToItem.Remove(packet.Player);
    }
}
