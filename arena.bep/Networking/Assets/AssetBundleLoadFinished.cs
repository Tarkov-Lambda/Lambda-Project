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

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct AssetBundleLoadFinishedPacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public string id;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<AssetBundleLoadFinishedPacket>(reader);
}

// Player tells server they are done loading this specific batch of asset bundles
public class AssetBundleLoadFinishedPacketHandler : PacketHandler<AssetBundleLoadFinishedPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(string id)
    {
        var packet = new AssetBundleLoadFinishedPacket
        {
            id = id,
            Player = H.MainPlayer
        };

        DispatchPacket(packet);
    }

    protected override void ProcessApprovedPacket(ref AssetBundleLoadFinishedPacket packet, NetPeer peer)
    {
        MutateApprovedPacket(ref packet, peer);
        ApplyInternal(packet, peer);
    }

    protected override async void Apply(AssetBundleLoadFinishedPacket packet, NetPeer peer)
    {
        var assetBundleLoadProgressDictionary = Singleton<AssetBundleLoadPacketHandler>.Instance.AssetBundleLoadProgress;
        var assetBundleLoadProgress = assetBundleLoadProgressDictionary[packet.id];

        if (assetBundleLoadProgress != null)
        {
            var playerLoadState = assetBundleLoadProgress.FirstOrDefault(p => p.player == packet.Player);
            playerLoadState.hasLoaded = true;

            if (assetBundleLoadProgress.All(p => p.hasLoaded == true))
            {
                assetBundleLoadProgressDictionary.Remove(packet.id);
            }
        }
    }
}