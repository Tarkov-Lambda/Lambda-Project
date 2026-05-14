using EFT;
using Lambda.Shared.Models;
using MemoryPack;

namespace Lambda.Core.Networking;


[MemoryPackable]
public partial struct SendMessagePacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public ChatMessageScope scope;
    public string msg;
}

public class SendMessagePacketWarden : LambdaPacketWarden<SendMessagePacket>
{
    public void Send(ChatMessageScope scope, string msg)
    {
        var packet = new SendMessagePacket
        {
            Player = H.MainPlayer,
            scope = scope,
            msg = msg
        };
        DispatchPacket(packet);
    }

    protected override void ApplyOptimistically(SendMessagePacket packet)
    {
        
    }

    protected override void Apply(SendMessagePacket packet, int peerId)
    {
        if (packet.Player.IsYourPlayer) return;

           
    }
}