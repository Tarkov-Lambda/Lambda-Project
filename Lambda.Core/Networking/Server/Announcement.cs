using EFT;
using Fika.Core.Main.Players;
using MemoryPack;
using PacketWarden;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct AnnouncementPacket : IPacket
{
    public string msg;
}

public class AnnouncementPacketWarden : LambdaPacketWarden<AnnouncementPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.Admin;

    public void Send(string msg)
    {
        var packet = new AnnouncementPacket
        {
            msg = msg
        };
        DispatchPacket(packet);
    }

    public void SendToPlayer(Player player, string msg)
    {
        var packet = new AnnouncementPacket
        {
            msg = msg
        };

        FikaPlayer fikaPlayer = player as FikaPlayer;

        DispatchPacket(packet, fikaPlayer.NetId);
    }

    protected override void Apply(AnnouncementPacket packet, int peerId)
    {

    }
}