using System.Collections.Generic;
using Comfort.Common;
using EFT;
using MemoryPack;

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
        var packet = new AssetBundleLoadFinishedPacket { Player = H.MainPlayer, id = id };

        DispatchPacket(ref packet);
    }

    // server only application
    protected override void ProcessApprovedPacket(
        ref AssetBundleLoadFinishedPacket packet,
        int peerId
    )
    {
        MutateApprovedPacket(ref packet, peerId);
        ApplyInternal(packet, peerId);
    }

    protected override void Apply(AssetBundleLoadFinishedPacket packet, int peerId)
    {
        var pendingLoads = Singleton<AssetBundleLoadPacketWarden>.Instance.PendingLoads;

        if (packet.id == null || !pendingLoads.TryGetValue(packet.id, out HashSet<Player> pending))
            return;

        pending.Remove(packet.Player);

        if (pending.Count == 0)
            pendingLoads.Remove(packet.id);
    }
}
