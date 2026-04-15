using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Economy;
using PacketHandler;
using PacketHandler.RateLimiting;
using ifp.arena.shared.Models;
using ifp.arena.bep.Core.Gamemode;

namespace ifp.arena.bep.networking;

public struct AskForMoneyPacket : INetSerializable, IAuthoredPacket
{
    public Player Player { get; set; }
    public string ItemBsgId;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<AskForMoneyPacket>(reader);
}

public class AskForMoneyPacketHandler : PacketHandler<AskForMoneyPacket>
{
    public Dictionary<Player, string> playerToItem;

    public AskForMoneyPacketHandler()
    {
        playerToItem = [];

        EventBus.OnEnter += OnEnter;
    }

    public void OnEnter(MatchState matchState)
    {
        if (matchState == MatchState.RoundPrepare)
            playerToItem = [];
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
        // if game mode is not team based - return type shit

        var packet = new AskForMoneyPacket
        {
            Player = H.MainPlayer,
            ItemBsgId = itemBsgId
        };

        DispatchPacket(packet);
    }

    protected override async void LocalPredictApproved(AskForMoneyPacket packet)
    {
        playerToItem[packet.Player] = packet.ItemBsgId;
    }

    protected override async void WhenApproved(AskForMoneyPacket packet, NetPeer peer)
    {
        if (packet.Player.IsYourPlayer) return;

        playerToItem[packet.Player] = packet.ItemBsgId;
    }
}