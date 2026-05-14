using EFT;
using Lambda.Shared.Models;
using MemoryPack;
using PacketWarden.RateLimiting;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct ChatMessagePacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public ChatMessageScope scope;
    public string msg;
}

public class ChatMessagePacketWarden : LambdaPacketWarden<ChatMessagePacket>
{
    protected override bool ShouldNotifyAboutRejection => true;

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(0.25, RateLimitAction.Drop);

    public void Send(ChatMessageScope scope, string msg)
    {
        var packet = new ChatMessagePacket
        {
            Player = H.MainPlayer,
            scope = scope,
            msg = msg
        };
        DispatchPacket(packet);
    }

    protected override bool ValidatePacket(ChatMessagePacket packet, int peerId, out string rejectionReason)
    {
        rejectionReason = null;

        if (packet.msg.Length > 256)
        {
            rejectionReason = "Character Limit Exceeded";
            return false;
        }

        return true;
    }

    protected override void Apply(ChatMessagePacket packet, int peerId)
    {
        
    }
}