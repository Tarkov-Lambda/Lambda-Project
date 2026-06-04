using Comfort.Common;
using EFT;
using MemoryPack;
using EFT.InventoryLogic;
using Lambda.Core.Main.UI;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Lambda.Core.Main.AssetBundleHandling;
using Lambda.Shared.Models;
using Lambda.Core.Main.Economy;
using Lambda.Core.Main;
using Fika.Core.Main.Players;
using Lambda.Core.Main.Gamemode;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct PlayerReadinessPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public PlayerReadinessState readyState;
    public string clanTag;

    [MemoryPackAllowSerialize]
    public Dictionary<ShopItem, Item> buySelection;
}

public class PlayerReadinessPacketWarden : LambdaPacketWarden<PlayerReadinessPacket>
{
    protected override bool ShouldApplyBeforeArenaInitialized => true;

    public void Send(PlayerReadinessState readyState)
    {
        if (H.IsHeadless) return;

        var packet = new PlayerReadinessPacket
        {
            Player = H.MainPlayer,
            readyState = readyState,
            clanTag = LambdaPlugin.ClanTag.Value
        };

        if (readyState is PlayerReadinessState.Connected)
        {
            packet.buySelection = [];
            foreach (var shopItem in BuyMenuSelection.GetAllShopItems())
            {
                packet.buySelection.Add(shopItem, PresetItemsCache.Instance.GetPresetItem(shopItem.bsgId));
            }
        }

        DispatchPacket(ref packet);
    }

    public void SendForPlayer(Player targetPlayer, PlayerReadinessState readyState)
    {
        var packet = new PlayerReadinessPacket
        {
            Player = targetPlayer,
            readyState = readyState,
        };
        DispatchPacket(ref packet);
    }

    protected override void MutateApprovedPacket(ref PlayerReadinessPacket packet, int peerId)
    {
        if (packet.buySelection != null)
        {
            PlayerContext pContext = H.GetPlayerContext(packet.Player);
            var defaultEquipment = DefaultEquipmentManager.CapturePreset(packet.Player);
            pContext.SetDefaultItems(defaultEquipment);
            pContext.SetBuySelection(packet.buySelection);

            List<Item> playerShopItems = new();
            foreach (var shopItem in packet.buySelection)
            {
                playerShopItems.Add(shopItem.Value);
            }

            // this logic is here to save on bandwidth
            bool isLateJoiner = H.Session.matchState >= MatchState.WarmupEnd;
            if (isLateJoiner)
            {
                D.Log("isLateJoiner");
                List<Item> unloadedItems = RuntimeBundleLoader.Instance.AddToCacheAndGetDelta(playerShopItems);
                D.Log("List<Item> unloadedItems");
                if (unloadedItems.Count > 0)
                {
                    Singleton<AssetBundleLoadPacketWarden>.Instance.BroadcastMidJoinersItems(unloadedItems);
                }
                D.Log("Singleton<AssetBundleLoadPacketWarden>.Instance.BroadcastMidJoinersItems");
            }
            else
            {
                RuntimeBundleLoader.Instance.AddToCache(playerShopItems);
            }

            packet.buySelection = null;
        }
    }

    protected override void Apply(PlayerReadinessPacket packet, int peerId)
    {
        if (!IsArenaReady) return;

        // does not respect reconnects, todo later
        bool isLateJoiner = H.Session.matchState >= MatchState.WarmupEnd;

        PlayerContext playerContext = H.GetPlayerContext(packet.Player);

        if (packet.clanTag != null)
        {
            playerContext.SetClanTag(packet.clanTag);
        }

        if (packet.Player.TryGetHandsResourceKey(out ResourceKey handsBundle))
        {
            List<ResourceKey> handsBundleCollection = [handsBundle];
            handsBundleCollection.LoadBundles().Forget();
        }

        playerContext.ChangeReadiness(packet.readyState);

        if (H.IsServer)
        {
            if (packet.Player is ObservedPlayer observedPlayer)
            {
                Singleton<ReconnectSnapshotterResetPacketWarden>.Instance.Send(observedPlayer);
            }

            // In case a player is reporting they are connected mid session (reconnects, new joins)
            if (isLateJoiner)
            {
                if (packet.readyState == PlayerReadinessState.Connected)
                {
                    // get the player up to speed
                    Singleton<SessionStartPacketWarden>.Instance.SendToPeer(peerId);

                    // Singleton<FactionChangePacketWarden>.Instance.SendForPlayer(packet.Player, Faction.Spectator);
                }
                else if (packet.readyState == PlayerReadinessState.Ready)
                {
                    playerContext.SetHardReset();
                    Singleton<SessionManagerSyncPacketWarden>.Instance.SendToPeer(peerId);
                    Singleton<MatchStateSyncPacketWarden>.Instance.SendToLateJoiner(peerId);
                    // Singleton<GameplayVariablesSyncPacketWarden>.Instance.SendToPeer(peerId);
                    // holy size but who gives a fuck
                    foreach (var player in H.AllPlayers)
                    {
                        Singleton<EquipmentResyncPacketWarden>.Instance.SendToPeer(player, peerId);
                    }
                }
            }
        }

        if (packet.Player.IsYourPlayer && packet.readyState == PlayerReadinessState.Connected && !LambdaPlugin.Password.Value.IsNullOrEmpty())
        {
            Singleton<AdminLoginPacketWarden>.Instance.Send();
        }
    }
}