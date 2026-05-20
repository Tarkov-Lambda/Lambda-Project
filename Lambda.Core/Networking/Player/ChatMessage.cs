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
        if (packet.msg.Length > 256)
        {
            rejectionReason = "Character Limit Exceeded";
            return false;
        }

        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }

    protected override void ProcessApprovedPacket(ref ChatMessagePacket packet, int peerId)
    {
        if (packet.msg.StartsWith("!") && packet.Player.GetContext().IsAdmin)
        {
            HandleCommandMessage(packet);
            DispatchPacket(packet, peerId);
        }
        else
        {
            base.ProcessApprovedPacket(ref packet, peerId);
        }
    }

    // Handled in ChatController
    protected override void Apply(ChatMessagePacket packet, int peerId) { }

    private void HandleCommandMessage(ChatMessagePacket msg)
    {
        
    }
}