using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using PacketHandler;
using ifp.arena.shared;
using MemoryPack;
using EFT.InventoryLogic;
using ifp.arena.bep.Core.UI;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.shared.Models;
using ifp.arena.bep.Core.Economy;
using arena.ui;
using EFT.UI;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PlayerReadinessPacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public PlayerReadinessState readyState;

    public Dictionary<ShopItem, Item> buySelection;
    public List<Item> presetItems; // only sent by the player on the initial connection

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<PlayerReadinessPacket>(reader);
}

public class PlayerReadinessPacketHandler : PacketHandler<PlayerReadinessPacket>
{
    public void Send(PlayerReadinessState readyState)
    {
        if (H.IsHeadless) return;

        var packet = new PlayerReadinessPacket
        {
            Player = H.MainPlayer,
            readyState = readyState
        };

        if (readyState is PlayerReadinessState.Connected)
        {
            packet.presetItems = PresetBundleHandler.Instance.itemsToLoad;
            packet.buySelection = [];
            foreach (var shopItem in BuyMenuSelection.GetAllShopItems())
            {
                packet.buySelection[shopItem] = PresetItemsCache.Instance.GetPresetItem(shopItem.bsgId);
            }
        }

        DispatchPacket(packet);
    }

    public void SendForPlayer(Player targetPlayer, PlayerReadinessState readyState)
    {
        var packet = new PlayerReadinessPacket
        {
            Player = targetPlayer,
            readyState = readyState,
        };
        DispatchPacket(packet);
    }

    protected override void MutateApprovedPacket(ref PlayerReadinessPacket packet, NetPeer peer)
    {
        if (packet.presetItems != null)
        {
            var playerScore = H.GetPlayerScore(packet.Player);
            playerScore.SetBuySelection(packet.buySelection);
            PresetBundleHandler.Instance.AddToCache(packet.presetItems);

            // other clients don't need this info
            packet.presetItems = null;
            packet.buySelection = null;
        }
    }

    protected override void Apply(PlayerReadinessPacket packet, NetPeer peer)
    {
        PlayerScore playerScore = H.GetPlayerScore(packet.Player);
        if (playerScore == null)
        {
            H.Scoreboard[packet.Player.Id] = new PlayerScore(packet.Player.Id);
            playerScore = H.Scoreboard[packet.Player.Id];

            if (packet.readyState == PlayerReadinessState.Ready)
            {
                playerScore.ChangeProgress(100f);
            }
        }

        playerScore.ChangeReadiness(packet.readyState);

        if (!H.IsClient)
        {
            // In case a player is reporting they are connected mid session (reconnects, new joins)
            if (H.Session?.matchState > MatchState.WarmupEnd && packet.readyState == PlayerReadinessState.Connected)
            {

                // 
                if (!H.Scoreboard.ContainsKey(packet.Player.Id))
                {
                    H.Scoreboard[packet.Player.Id] = new PlayerScore(packet.Player.Id);
                    H.GetPlayerScore(packet.Player.Id).ChangeFaction(Faction.Spectator);
                }

                Singleton<SessionStartPacketHandler>.Instance.SendToPeer(peer);
                Singleton<SessionManagerSyncPacketHandler>.Instance.SendToPeer(peer);
                Singleton<MatchStateSyncPacketHandler>.Instance.SendToLateJoiner(peer);
            }
        }

        if (packet.Player.IsYourPlayer && packet.readyState == PlayerReadinessState.Connected)
        {
            Singleton<AdminLoginPacketHandler>.Instance.Send();
        }
    }
}