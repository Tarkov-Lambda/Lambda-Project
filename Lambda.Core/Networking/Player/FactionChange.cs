using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Main.UI;
using MemoryPack;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct FactionChangePacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public Faction faction;

    [MemoryPackAllowSerialize]
    public Item armband;

    [MemoryPackAllowSerialize]
    public ItemAddress armbandAddress;
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

        if (H.Session.matchState < MatchState.RoundPrepare) return true;

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

    protected override void MutateApprovedPacket(ref FactionChangePacket packet, int peerId)
    {
        if (H.Gamemode is IGMTeam)
        {
            string selectedArmband = packet.faction == Faction.CT ? Hardcode.ARMBAND_CT : Hardcode.ARMBAND_T;
            packet.armband = PresetItemsCache.Instance.GetPresetItem(selectedArmband).CloneItem() as ArmBandItemClass;

            var armbandSlot = packet.Player.Equipment.GetSlot(EquipmentSlot.ArmBand);
            packet.armbandAddress = armbandSlot.CreateItemAddress();
        }
    }

    protected override void Apply(FactionChangePacket packet, int peerId)
    {
        if (packet.Player.IsYourPlayer) _cts?.Cancel();

        H.GetPlayerScore(packet.Player)?.ChangeFaction(packet.faction);

        if (H.Gamemode is IGMTeam)
        {
            var armbandSlot = packet.Player.Equipment.GetSlot(EquipmentSlot.ArmBand);

            if (armbandSlot.ContainedItem != null)
            {
                var oldArmband = armbandSlot.ContainedItem;
                packet.armbandAddress.RemoveWithoutRestrictions(oldArmband);
                packet.armbandAddress.RaiseForceRemove(oldArmband, packet.Player);
            }

            packet.armbandAddress.AddWithoutRestrictions(packet.armband);
            packet.armbandAddress.RaiseForceAdd(packet.armband, packet.Player);
        }
    }
}