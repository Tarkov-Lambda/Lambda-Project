using EFT;
using Lambda.Core.Networking.Commands;
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

        if (ChatCommandInterceptor.TryHandleLocal(msg))
        {
            ApplyInternal(packet, Network.NetId);
            return;
        }

        DispatchPacket(ref packet);
    }

    protected override bool ValidatePacket(ChatMessagePacket packet, int peerId, out string rejectionReason)
    {
        if (packet.msg.Length > 132)
        {
            rejectionReason = "Character Limit Exceeded";
            return false;
        }

        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }

    protected override void ProcessApprovedPacket(ref ChatMessagePacket packet, int peerId)
    {
        // all exclamation mark commands are serverside
        // the client sees their command message but others do not
        if (packet.msg.StartsWith("!"))
        {
            ChatCommandInterceptor.HandleServer(packet.Player, peerId, packet.msg);
        }
        else
        {
            base.ProcessApprovedPacket(ref packet, peerId);
        }
    }

    /// Handled in ChatController
    protected override void Apply(ChatMessagePacket packet, int peerId) { }
}