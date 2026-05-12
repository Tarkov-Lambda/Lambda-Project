using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using PacketHandler;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct RaiseErrorPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public string error;
    public bool isForAdmin;
}

public class RaiseErrorPacketHandler : LambdaPacketHandler<RaiseErrorPacket>
{
    public void Send(string error, bool isForAdmin = true)
    {
        var packet = new RaiseErrorPacket
        {
            error = error,
            isForAdmin = isForAdmin
        };

        if (!H.IsHeadless)
        {
            packet.Player = H.MainPlayer;
        }

        DispatchPacket(packet);
    }

    protected override void Apply(RaiseErrorPacket packet, int peerId)
    {
        if (packet.Player == null)
        {
            D.Notify($"Headless Server got error: {packet.error}");
            return;
        }

        bool shouldNotify = !packet.isForAdmin || H.IsHeadless || H.MainPlayerScore.IsAdmin;

        if (shouldNotify)
        {
            D.Notify($"{packet.Player.Profile.Nickname} got error: {packet.error}");
        }
    }
}