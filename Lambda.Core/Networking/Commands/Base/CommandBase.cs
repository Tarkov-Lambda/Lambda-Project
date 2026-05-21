using System;
using Comfort.Common;
using EFT;
using PacketWarden;

namespace Lambda.Core.Networking.Commands;

public enum CommandTarget
{
    ClientOnly,
    ServerOnly,
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class ChatCommandAttribute(string name, string description, CommandTarget target, PacketAuthority authority = PacketAuthority.Admin) : Attribute
{
    public string Name { get; } = name.ToLowerInvariant();
    public string Description { get; } = description;
    public CommandTarget Target { get; } = target;
    public PacketAuthority Authority { get; } = authority;
}

public class CommandContext
{
    public Player Sender { get; }
    public int PeerId { get; }
    public string RawMessage { get; }
    public string[] Args { get; }

    public CommandContext(Player sender, int peerId, string rawMessage, string[] args)
    {
        Sender = sender;
        RawMessage = rawMessage;
        Args = args;
        PeerId = peerId;
    }

    public void Reply(string message)
    {
        if (H.IsServer)
        {
            Singleton<ServerMessagePacketWarden>.Instance.SendToPeer(message, PeerId);
        }
        else
        {
            D.Notify(message);
        }
    }

    public void Announce(string message)
    {
        Singleton<ServerMessagePacketWarden>.Instance.Send(message);
    }
}