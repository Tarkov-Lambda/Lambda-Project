using PacketWarden;
using MemoryPack;
using EFT;
using Cysharp.Threading.Tasks;
using Comfort.Common;
using System.Linq;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct AssetBundleLoadFinishedPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public string id;
}

// Player tells server they are done loading this specific batch of asset bundles
public class AssetBundleLoadFinishedPacketWarden : LambdaPacketWarden<AssetBundleLoadFinishedPacket>
{
    public void Send(string id)
    {
        var packet = new AssetBundleLoadFinishedPacket
        {
            id = id,
            Player = H.MainPlayer
        };

        DispatchPacket(packet);
    }

    // server only application
    protected override void ProcessApprovedPacket(ref AssetBundleLoadFinishedPacket packet, int peerId)
    {
        MutateApprovedPacket(ref packet, peerId);
        ApplyInternal(packet, peerId);
    }

    protected override async void Apply(AssetBundleLoadFinishedPacket packet, int peerId)
    {
        var assetBundleLoadProgressDictionary = Singleton<AssetBundleLoadPacketWarden>.Instance.AssetBundleLoadProgress;
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