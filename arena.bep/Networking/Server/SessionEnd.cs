using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;
using ifp.arena.bep.Core.AssetBundleHandling;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct SessionStopPacket : INetSerializable
{
    public bool isItTrue;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<SessionStopPacket>(reader);
}

public class SessionStopPacketHandler : PacketHandler<SessionStopPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.Admin;

    protected override bool ShouldNotifyAboutRejection => base.ShouldNotifyAboutRejection;

    public void Send()
    {
        var packet = new SessionStopPacket { isItTrue = true }; // true

        DispatchPacket(packet);
    }

    protected override async void Apply(SessionStopPacket packet, NetPeer peer)
    {
        H.Arena.ChangeState(MatchState.None);
        MapAssetBundleHandler.Instance.UnloadAll();
    }
}