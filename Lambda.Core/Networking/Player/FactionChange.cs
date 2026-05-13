using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core.Main.Utils;
using MemoryPack;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct FactionChangePacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public Faction faction;
}

public class FactionChangePacketWarden : LambdaPacketWarden<FactionChangePacket>
{
    public CancellationTokenSource _cts { get; private set; }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.Dispose();
    }

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

    protected override bool ValidatePacket(FactionChangePacket packet, int peerId, out string rejectionReason)
    {
        if (!CanChangeFaction(H.GetPlayerScore(packet.Player.Id), packet.faction))
        {
            rejectionReason = "You can not swap factions in the current phase";
            return false;
        }

        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }

    protected override void Apply(FactionChangePacket packet, int peerId)
    {
        if (packet.Player.IsYourPlayer) _cts?.Cancel();

        H.GetPlayerScore(packet.Player)?.ChangeFaction(packet.faction);
    }
}