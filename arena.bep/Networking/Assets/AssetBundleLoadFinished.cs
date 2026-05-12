using PacketHandler;
using MemoryPack;
using EFT;
using Cysharp.Threading.Tasks;
using Comfort.Common;
using System.Linq;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct AssetBundleLoadFinishedPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public string id;
}

// Player tells server they are done loading this specific batch of asset bundles
public class AssetBundleLoadFinishedPacketHandler : LambdaPacketHandler<AssetBundleLoadFinishedPacket>
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

    protected override void ProcessApprovedPacket(ref AssetBundleLoadFinishedPacket packet, int peerId)
    {
        MutateApprovedPacket(ref packet, peerId);
        ApplyInternal(packet, peerId);
    }

    protected override async void Apply(AssetBundleLoadFinishedPacket packet, int peerId)
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