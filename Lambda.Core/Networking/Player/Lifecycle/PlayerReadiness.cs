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

    [MemoryPackAllowSerialize]
    public Dictionary<EquipmentSlot, Item> defaultItems;
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
            clanTag = Plugin.ClanTag.Value
        };

        if (readyState is PlayerReadinessState.Connected)
        {
            packet.defaultItems = ClientEquipmentManager.Instance.RecordedItems;
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
            pContext.SetDefaultItems(packet.defaultItems);
            pContext.SetBuySelection(packet.buySelection);

            // we only need to add buy menu stuff into the cache
            // because defaultItems is literally the equipment that player has spawned in the raid with
            foreach (var shopItem in packet.buySelection)
            {
                RuntimeBundleLoader.Instance.AddToCache(shopItem.Value);
            }

            packet.buySelection = null;
        }
    }

    protected override void Apply(PlayerReadinessPacket packet, int peerId)
    {
        if (packet.Player == null) return;
        if (!IsArenaReady && !packet.Player.IsYourPlayer) return;

        bool isNewPlayer = !H.Scoreboard.ContainsKey(packet.Player.Id);
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

        if (isNewPlayer)
        {
            if (packet.readyState == PlayerReadinessState.Ready)
            {
                playerContext.ChangeProgress(100f);
                playerContext.ChangeFaction(Faction.Spectator);
                playerContext.SetHardReset();
            }
        }

        playerContext.ChangeReadiness(packet.readyState);

        if (H.IsServer)
        {
            if (packet.Player is ObservedPlayer observedPlayer)
            {
                Singleton<ReconnectSnapshotterResetPacketWarden>.Instance.Send(observedPlayer);
            }

            // In case a player is reporting they are connected mid session (reconnects, new joins)
            if (H.Arena.gamemode != null && H.Session.matchState != MatchState.None)
            {
                if (packet.readyState == PlayerReadinessState.Connected)
                {
                    // get the player up to speed
                    Singleton<SessionStartPacketWarden>.Instance.SendToPeer(peerId);
                    Singleton<SessionManagerSyncPacketWarden>.Instance.SendToPeer(peerId);
                    Singleton<MatchStateSyncPacketWarden>.Instance.SendToLateJoiner(peerId);
                    Singleton<GameplayVariablesSyncPacketWarden>.Instance.SendToPeer(peerId);
                    // holy size but who gives a fuck
                    foreach (var player in H.AllPlayers)
                    {
                        Singleton<EquipmentResyncPacketWarden>.Instance.SendToPeer(player, peerId);
                    }
                }
            }
        }

        if (packet.Player.IsYourPlayer && packet.readyState == PlayerReadinessState.Connected && !Plugin.Password.Value.IsNullOrEmpty())
        {
            Singleton<AdminLoginPacketWarden>.Instance.Send();
        }
    }
}