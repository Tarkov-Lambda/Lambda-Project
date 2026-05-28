using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core.Main.Players;
using MemoryPack;
using PacketWarden;
using UnityEngine;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct ServerMessagePacket : IPacket
{
    public string msg;
    public Faction? specificFaction;
}

// Handles all server originated messages (Session, Economy, etc)
public class ServerMessagePacketWarden : LambdaPacketWarden<ServerMessagePacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public Action<ServerMessagePacket> OnRejectionMessageInChat;

    public void Send(string msg)
    {
        var packet = new ServerMessagePacket
        {
            msg = msg
        };
        DispatchPacket(ref packet);
    }

    public void SendToPeer(string msg, int peerId)
    {
        var packet = new ServerMessagePacket
        {
            msg = msg
        };

        DispatchPacket(ref packet, peerId);
    }

    public void SendToFaction(Faction faction, string msg)
    {
        var packet = new ServerMessagePacket
        {
            msg = msg,
            specificFaction = faction
        };

        DispatchPacket(ref packet);
    }

    // Handled in ChatController
    protected override void Apply(ServerMessagePacket packet, int peerId) { }
}