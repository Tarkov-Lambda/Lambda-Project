using System.Threading.Tasks;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core.Main.Players;
using MemoryPack;
using PacketWarden;
using UnityEngine;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct ServerRefreshPacket : IPacket
{
    public string msg;
    public Faction specificFaction;
}

// idk if I need this TBH
public class ServerRefreshPacketWarden : LambdaPacketWarden<ServerRefreshPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(string msg)
    {
        var packet = new ServerRefreshPacket
        {
            msg = msg
        };
        DispatchPacket(ref packet);
    }

    protected override void Apply(ServerRefreshPacket packet, int peerId)
    {
        Singleton<PlayerReadinessPacketWarden>.Instance.Send(PlayerReadinessState.Connected);
    }
}