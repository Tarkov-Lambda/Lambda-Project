using System;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct FactionChangePacket : INetSerializable
{
    [MemoryPackAllowSerialize]
    public Player player { get; set; }

    public Faction faction;

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<FactionChangePacket>(reader);
}

public class FactionChangePacketHandler : PacketHandler<FactionChangePacket>
{
    bool CanChangeFaction(Faction faction)
    {
        if (faction == Faction.Spectator) return true;

        if (H.Session.matchState <= MatchState.RoundPrepare) return true;

        return false;
    }

    public async void Send(Faction faction)
    {
        var packet = new FactionChangePacket { faction = faction };

        if (FikaBackendUtils.IsSpectator) packet.faction = Faction.Spectator;

        await UniTask.WaitUntil(() => CanChangeFaction(packet.faction));

        RequestSend(packet);
    }

    protected override bool ServerValidation(ref FactionChangePacket packet, NetPeer netPeer)
    {
        if (!CanChangeFaction(packet.faction)) return false;

        return base.ServerValidation(ref packet, netPeer);
    }

    protected override void WhenApproved(FactionChangePacket packet, NetPeer peer)
    {
        H.GetPlayerScore(packet.player)?.faction = packet.faction;
    }
}