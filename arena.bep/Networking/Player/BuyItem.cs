using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Economy;
using PacketHandler;
using ifp.arena.shared.Models;
using System.Linq;
using Fika.Core.Main.Players;
using Fika.Core.Main.ObservedClasses;
using HarmonyLib;
using System.Reflection;
using System.Collections.Concurrent;
using ifp.arena.bep.Core.UI;

namespace ifp.arena.bep.networking;

public struct BuyItemPacket : INetSerializable, IAuthoredPacket
{
    public Player Player { get; set; }
    public ItemPlacement placement;
    public Item Item;

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPlayer(Player);
        writer.Put(placement);
        writer.PutItem(Item);
    }

    public void Deserialize(NetDataReader reader)
    {
        Player = reader.GetPlayer();
        placement = reader.GetItemPlacement(Player);
        Item = reader.GetItem();
    }
}

public class BuyItemPacketHandler : PacketHandler<BuyItemPacket>
{
    protected override bool ShouldNotifyAboutRejection => true;



    public void Send(Item item, ItemPlacement placement)
    {
        var packet = new BuyItemPacket
        {
            Player = H.MainPlayer,
            Item = item,
            placement = placement,
        };

        DispatchPacket(packet);
    }

    protected override bool EvaluatePacket(ref BuyItemPacket packet, NetPeer peer, out string rejectionReason)
    {
        if (H.Session.matchState != MatchState.Cleanup)
        {
            if (BuyMenuSelection.TryGetItemData(packet.Item.TemplateId, out ShopItem itemData))
            {
                var playerScore = packet.Player.GetScore();
                if (playerScore.Money < itemData.price)
                {
                    rejectionReason = "You don't have enough money";
                    return false;
                }
            }
        }

        var placement = AU.GetItemPlacement(packet.Item, packet.Player);
        if (packet.placement.Address != placement.Address)
        {
            packet.placement = placement;
        }

        D.Log(placement.Address.Container.CanAccept(packet.Item).ToString());

        if (!placement.Address.Container.CanAccept(packet.Item))
        {
            rejectionReason = "Container can not accept this item";
            return false;
        }

        // Server deals with cloning
        packet.Item = packet.Item.CloneItem();

        rejectionReason = null;
        return true;
    }


    protected override void WhenApproved(BuyItemPacket packet, NetPeer peer)
    {
        if (H.Session.matchState != MatchState.Cleanup)
        {
            if (BuyMenuSelection.TryGetItemData(packet.Item.TemplateId, out ShopItem itemData))
            {
                H.GetPlayerScore(packet.Player.Id).SpendMoney(itemData.price);
            }
        }

        IU.WhenApprovedGiveItem(packet.Item, packet.Player, packet.placement);
    }

    protected override void WhenRejected(BuyItemPacket packet, NetPeer peer)
    {
        if (BuyMenuSelection.TryGetItemData(packet.Item.TemplateId, out ShopItem itemData))
        {
            H.MainPlayerScore.AddMoney(itemData.price);
        }
    }
}