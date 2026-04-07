using System;
using System.Threading;
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
public partial struct FactionChangePacket : INetSerializable, AuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player player { get; set; }

    public Faction faction;

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<FactionChangePacket>(reader);
}

public class FactionChangePacketHandler : PacketHandler<FactionChangePacket>
{
    public CancellationTokenSource _cts { get; private set; }

    bool CanChangeFaction(PlayerScore playerScore, Faction faction)
    {
        if (!playerScore.isAlive) return true;

        if (faction == Faction.Spectator) return true;

        if (H.Session.matchState < MatchState.RoundAction) return true;

        return false;
    }

    public async void Send(Faction faction)
    {
        var packet = new FactionChangePacket { faction = faction };

        if (FikaBackendUtils.IsSpectator) packet.faction = Faction.Spectator;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            if (!CanChangeFaction(H.MainPlayerScore, packet.faction))
            {
                D.Notify("You will change factions at the start of the next round.");
                await UniTask.WaitUntil(() => CanChangeFaction(H.MainPlayerScore, packet.faction), cancellationToken: _cts.Token);
            }

            RequestSend(packet);
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    protected override bool ServerValidation(ref FactionChangePacket packet, NetPeer netPeer)
    {
        if (!CanChangeFaction(H.GetPlayerScore(packet.player.Id), packet.faction)) return false;

        return base.ServerValidation(ref packet, netPeer);
    }

    protected override void WhenApproved(FactionChangePacket packet, NetPeer peer)
    {
        if (packet.player.IsYourPlayer) _cts?.Cancel();

        H.GetPlayerScore(packet.player)?.ChangeFaction(packet.faction);
    }
}