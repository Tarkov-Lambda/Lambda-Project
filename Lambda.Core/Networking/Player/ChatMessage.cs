using EFT;
using Lambda.Shared.Models;
using MemoryPack;

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

    protected override void ApplyOptimistically(ChatMessagePacket packet)
    {
        
    }

    protected override void Apply(ChatMessagePacket packet, int peerId)
    {
        if (packet.Player.IsYourPlayer) return;

           
    }
}