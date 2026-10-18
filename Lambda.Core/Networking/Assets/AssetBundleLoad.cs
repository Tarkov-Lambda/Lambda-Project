using System;
using System.Collections.Generic;
using System.Threading;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using Lambda.Core.Main.AssetBundleHandling;
using MemoryPack;
using PacketWarden;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct AssetBundleLoadPacket : IPacket
{
    public string id;
    public List<string> templateIds;
    public bool replyWhenLoaded;
}

// Currently this is only used for loading asset bundles for late joining player's buy menu
// Whilst we are awaiting until everyone says they loaded the assets on the server
// we are not actually gating the late-joiner from spawning and buying something
// this is kind of bad but I don't have time to fix it
public class AssetBundleLoadPacketWarden : LambdaPacketWarden<AssetBundleLoadPacket>
{
    private readonly CancellationTokenSource _cts = new();

    public override void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        base.Dispose();
    }

    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;
    protected override bool ShouldApplyBeforeArenaInitialized => true;

    // SERVER ONLY: batch id -> players that have not reported back yet
    public Dictionary<string, HashSet<Player>> PendingLoads = new();

    public async UniTask SendAndAwaitFullReadiness(List<string> templateIds)
    {
        string id = Guid.NewGuid().ToString();

        var pending = new HashSet<Player>();
        foreach (var player in H.AllPlayers)
        {
            if (H.IsHeadless && player.IsYourPlayer)
                continue;
            pending.Add(player);
        }

        PendingLoads[id] = pending;

        var packet = new AssetBundleLoadPacket
        {
            id = id,
            templateIds = templateIds,
            replyWhenLoaded = true,
        };

        DispatchPacket(ref packet);

        await UniTask.WaitUntil(() => !PendingLoads.ContainsKey(id), cancellationToken: _cts.Token);
    }

    public void SendToLateJoiner(Player player, List<string> templateIds) =>
        SendToLateJoiner(Network.GetPeerIdByPlayer(player), templateIds);

    public void SendToLateJoiner(int peerId, List<string> templateIds)
    {
        var packet = new AssetBundleLoadPacket
        {
            templateIds = templateIds,
            replyWhenLoaded = false,
        };

        DispatchPacket(ref packet, peerId);
    }

    public void BroadcastMidJoinersItems(List<string> templateIds)
    {
        var packet = new AssetBundleLoadPacket
        {
            templateIds = templateIds,
            replyWhenLoaded = false,
        };

        DispatchPacket(ref packet);
    }

    protected override async void Apply(AssetBundleLoadPacket packet, int peerId)
    {
        RuntimeBundleLoader.Instance.AddToCache(packet.templateIds);
        await RuntimeBundleLoader.Instance.LoadEverythingInCache();

        if (!H.IsHeadless && packet.replyWhenLoaded)
        {
            Singleton<AssetBundleLoadFinishedPacketWarden>.Instance.Send(packet.id);
        }
    }
}
