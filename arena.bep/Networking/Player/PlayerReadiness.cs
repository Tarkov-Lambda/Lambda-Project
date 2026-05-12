using Comfort.Common;
using EFT;
using MemoryPack;
using EFT.InventoryLogic;
using ifp.arena.bep.Core.UI;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.shared.Models;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PlayerReadinessPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public PlayerReadinessState readyState;

    // presetItems is redundant as it can be derived form buySelection and defaultItems
    public List<Item> assetItems;
    public Dictionary<ShopItem, Item> buySelection;
    public Dictionary<EquipmentSlot, Item> defaultItems;
}

public class PlayerReadinessPacketHandler : LambdaPacketHandler<PlayerReadinessPacket>
{
    protected override bool ShouldApplyBeforeArenaInitialized => true;

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
            packet.assetItems = PresetBundleHandler.Instance.itemsToLoad;
            packet.buySelection = [];
            foreach (var shopItem in BuyMenuSelection.GetAllShopItems())
            {
                packet.buySelection.Add(shopItem, PresetItemsCache.Instance.GetPresetItem(shopItem.bsgId));
                packet.defaultItems = DefaultEquipmentManager.Instance.RecordedItems;
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

    protected override void MutateApprovedPacket(ref PlayerReadinessPacket packet, int peerId)
    {
        if (packet.assetItems != null)
        {
            var playerScore = H.GetPlayerScore(packet.Player);
            playerScore.SetDefaultItems(packet.defaultItems);
            playerScore.SetBuySelection(packet.buySelection);
            PresetBundleHandler.Instance.AddToCache(packet.assetItems);

            // other clients don't need this info
            packet.assetItems = null;
            packet.buySelection = null;
        }
    }

    protected override void Apply(PlayerReadinessPacket packet, int peerId)
    {
        bool isNewPlayer = !H.Scoreboard.ContainsKey(packet.Player.Id);
        PlayerScore playerScore = H.GetPlayerScore(packet.Player);

        if (packet.Player.TryGetHandsResourceKey(out ResourceKey handsBundle))
        {
            List<ResourceKey> handsBundleCollection = [handsBundle];
            handsBundleCollection.LoadBundles().Forget();
        }

        if (isNewPlayer)
        {
            if (packet.readyState == PlayerReadinessState.Ready)
            {
                playerScore.ChangeProgress(100f);
                playerScore.ChangeFaction(Faction.Spectator);
            }
        }

        playerScore.ChangeReadiness(packet.readyState);

        if (H.IsServer)
        {
            // In case a player is reporting they are connected mid session (reconnects, new joins)
            if (H.Arena.gamemode != null && H.Session.matchState != MatchState.None)
            {
                if (packet.readyState == PlayerReadinessState.Connected)
                {
                    // Broadcast updated itemsToLoad list
                    // whilst this is ridiculously wasteful, I know for a fact that it will work
                    // We should not forget about this, but for now it's fine
                    Singleton<AssetBundleLoadPacketHandler>.Instance.SendAndAwaitFullReadiness(PresetBundleHandler.Instance.itemsToLoad).Forget();

                    // get the player up to speed
                    Singleton<SessionStartPacketHandler>.Instance.SendToPeer(peerId);
                    Singleton<SessionManagerSyncPacketHandler>.Instance.SendToPeer(peerId);
                    Singleton<MatchStateSyncPacketHandler>.Instance.SendToLateJoiner(peerId);
                    Singleton<GameplayVariablesSyncPacketHandler>.Instance.SendToPeer(peerId);
                    // holy size but who gives a fuck
                    foreach (var player in H.AllPlayers)
                    {
                        Singleton<InventoryResyncPacketHandler>.Instance.SendToPeer(player, peerId);
                    }
                }
            }
        }

        if (packet.Player.IsYourPlayer && packet.readyState == PlayerReadinessState.Connected)
        {
            Singleton<AdminLoginPacketHandler>.Instance.Send();
        }
    }
}