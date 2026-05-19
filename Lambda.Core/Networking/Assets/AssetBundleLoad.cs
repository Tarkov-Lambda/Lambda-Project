using PacketWarden;
using MemoryPack;
using Lambda.Core.Main.AssetBundleHandling;
using System.Collections.Generic;
using EFT.InventoryLogic;
using EFT;
using Cysharp.Threading.Tasks;
using Comfort.Common;
using System;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct AssetBundleLoadPacket : IPacket
{
    public string id;

    [MemoryPackAllowSerialize]
    public List<Item> itemsToLoad;

    public bool replyWhenLoaded;
}

// SERVER ONLY
public struct PlayerAssetBundleLoadState
{
    public Player player;
    public bool hasLoaded;
}

// Currently this is only used for loading asset bundles for late joining player's buy menu
// Whilst we are awaiting until everyone says they loaded the assets on the server
// we are not actually gating the late-joiner from spawning and buying something
// this is kind of bad but I don't have time to fix it
public class AssetBundleLoadPacketWarden : LambdaPacketWarden<AssetBundleLoadPacket>
{
    public override void Dispose()
    {
        AssetBundleLoadProgress = null;
        base.Dispose();
    }

    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;
    protected override bool ShouldApplyBeforeArenaInitialized => true;

    public Dictionary<string, List<PlayerAssetBundleLoadState>> AssetBundleLoadProgress;

    public async UniTask SendAndAwaitFullReadiness(List<Item> itemsToLoad)
    {
        var packet = new AssetBundleLoadPacket
        {
            id = Guid.NewGuid().ToString(),
            itemsToLoad = itemsToLoad,
            replyWhenLoaded = true
        };

        AssetBundleLoadProgress[packet.id] = new List<PlayerAssetBundleLoadState>();

        foreach (var player in H.AllPlayers)
        {
            AssetBundleLoadProgress[packet.id].Add(new PlayerAssetBundleLoadState
            {
                player = player,
                hasLoaded = false
            });
        }

        DispatchPacket(packet);

        await UniTask.WaitUntil(() => !AssetBundleLoadProgress.ContainsKey(packet.id));

        return;
    }

    public void SendToLateJoiner(Player player, List<Item> itemsToLoad) => SendToLateJoiner(Network.GetPeerIdByPlayer(player), itemsToLoad);

    public void SendToLateJoiner(int peerId, List<Item> itemsToLoad)
    {
        var packet = new AssetBundleLoadPacket
        {
            id = Guid.NewGuid().ToString(),
            itemsToLoad = itemsToLoad,
            replyWhenLoaded = false
        };

        DispatchPacket(packet, peerId);
    }

    protected override async void Apply(AssetBundleLoadPacket packet, int peerId)
    {
        PresetBundleHandler.Instance.AddToCache(packet.itemsToLoad);
        await PresetBundleHandler.Instance.LoadEverythingInCache();

        if (!H.IsHeadless && packet.replyWhenLoaded)
        {
            Singleton<AssetBundleLoadFinishedPacketWarden>.Instance.Send(packet.id);
        }
    }
}