using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;
using ifp.arena.bep.Core.AssetBundleHandling;
using System.Collections.Generic;
using EFT.InventoryLogic;
using EFT;
using Cysharp.Threading.Tasks;
using Comfort.Common;
using System.Linq;
using System;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct AssetBundleLoadPacket : INetSerializable
{
    public string id;
    public List<Item> items;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<AssetBundleLoadPacket>(reader);
}

public struct PlayerAssetBundleLoadState
{
    public Player player;
    public bool hasLoaded;
}

// Currently this is only used for loading asset bundles for late joining player's buy menu
// Whilst we are awaiting until everyone says they loaded the assets on the server
// we are not actually gating the late-joiner from spawning and buying something
// this is kind of bad but I don't have time to fix it
public class AssetBundleLoadPacketHandler : PacketHandler<AssetBundleLoadPacket>
{
    public override void Dispose()
    {
        AssetBundleLoadProgress = null;
        base.Dispose();
    }

    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public Dictionary<string, List<PlayerAssetBundleLoadState>> AssetBundleLoadProgress;

    public async UniTask SendAndAwaitFullReadiness(List<Item> items)
    {
        var packet = new AssetBundleLoadPacket
        {
            id = Guid.NewGuid().ToString(),
            items = items
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

    protected override async void Apply(AssetBundleLoadPacket packet, NetPeer peer)
    {
        PresetBundleHandler.Instance.AddToCache(packet.items);
        await PresetBundleHandler.Instance.LoadEverythingInCache();

        if (!H.IsHeadless)
        {
            Singleton<AssetBundleLoadFinishedPacketHandler>.Instance.Send(packet.id);
        }
    }
}