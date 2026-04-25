using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using PacketHandler;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct FactionChangePacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public Faction faction;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<FactionChangePacket>(reader);
}

public class FactionChangePacketHandler : PacketHandler<FactionChangePacket>
{
    public CancellationTokenSource _cts { get; private set; }

    bool CanChangeFaction(PlayerScore playerScore, Faction faction)
    {
        if (!playerScore.IsAlive) return true;

        if (faction == Faction.Spectator) return true;

        if (H.Session.matchState < MatchState.RoundAction) return true;

        return false;
    }

    public async void Send(Faction faction)
    {
        var packet = new FactionChangePacket
        {
            Player = H.MainPlayer,
            faction = faction
        };

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

            DispatchPacket(packet);
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.Dispose();
    }

    protected override bool ValidatePacket(ref FactionChangePacket packet, NetPeer peer, out string rejectionReason)
    {
        rejectionReason = null;
        if (!CanChangeFaction(H.GetPlayerScore(packet.Player.Id), packet.faction)) return false;

        return true;
    }

    protected override void WhenApproved(FactionChangePacket packet, NetPeer peer)
    {
        if (packet.Player.IsYourPlayer) _cts?.Cancel();

        H.GetPlayerScore(packet.Player)?.ChangeFaction(packet.faction);
    }
}