using PacketHandler;
using MemoryPack;
using ifp.arena.bep.Core.AssetBundleHandling;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct SessionStopPacket : IPacket
{
    public bool isItTrue;
}

public class SessionStopPacketHandler : LambdaPacketHandler<SessionStopPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.Admin;

    protected override bool ShouldNotifyAboutRejection => base.ShouldNotifyAboutRejection;

    public void Send()
    {
        var packet = new SessionStopPacket { isItTrue = true }; // true

        DispatchPacket(packet);
    }

    protected override async void Apply(SessionStopPacket packet, int peerId)
    {
        H.Arena.ChangeState(MatchState.None);
        MapAssetBundleHandler.Instance.UnloadAll();
    }
}