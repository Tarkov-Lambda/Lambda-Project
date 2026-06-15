using System;
using EFT;
using Fika.Core.Main.Players;
using MemoryPack;
using PacketWarden;
using UnityEngine;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct TeleportToMePacketPacket : IPacket
{
    public Vector3 place;
}

public class TeleportToMePacketPacketWarden : LambdaPacketWarden<TeleportToMePacketPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.Admin;

    public void Send(Vector3 Place)
    {
        var packet = new TeleportToMePacketPacket
        {
            place = Place
        };
        DispatchPacket(ref packet);
    }

    protected override void Apply(TeleportToMePacketPacket packet, int peerId)
    {
        if (!H.IsHeadless) H.MainPlayer.Teleport(packet.place);
    }
}