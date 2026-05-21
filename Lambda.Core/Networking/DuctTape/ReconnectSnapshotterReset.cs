using System;
using System.Reflection;
using EFT;
using Fika.Core.Main.Players;
using HarmonyLib;
using MemoryPack;
using PacketWarden;
using UnityEngine;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct ReconnectSnapshotterResetPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }
}

public class ReconnectSnapshotterResetPacketWarden : LambdaPacketWarden<ReconnectSnapshotterResetPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(Player player)
    {
        var packet = new ReconnectSnapshotterResetPacket
        {
            Player = player
        };
        DispatchPacket(packet);
    }

    protected override void Apply(ReconnectSnapshotterResetPacket packet, int peerId)
    {
        try
        {
            if (packet.Player is ObservedPlayer observedPlayer)
            {
                observedPlayer.ResetSnapshotter();
            }
        }
        catch (Exception) { }

    }
}