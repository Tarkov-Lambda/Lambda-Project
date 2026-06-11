using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core;
using Lambda.Core.Main.Dying;
using Lambda.Core.Networking;
using System;
using System.Linq;
using System.Threading;

namespace Lambda.Core.Main.Gamemode;

// just kind of a "nothing is happening" state type beat
public class SharedNone : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.None;
    public override void OnEnter()
    {
        if (!H.IsHeadless)
        {
            Teleporter.Teleport(H.MainPlayer, "lobby", Faction.None);
            H.BetterAudio.FadeMixerVolume(H.BetterAudio.AudioMixerData.InGameVolumeMixer, 0f, 1f);
            PU.OpenEyes();
            H.MainPlayer.GetComponent<EftGamePlayerOwner>().Mute(false);
        }

        if (H.IsServer)
        {
            var allDisconnected = H.Scoreboard.Values.Where(p => p.ReadyState == PlayerReadinessState.Disconnected);

            foreach (var disconnectedPlayer in allDisconnected)
            {
                Singleton<PlayerReadinessPacketWarden>.Instance.SendForPlayer(disconnectedPlayer.player, PlayerReadinessState.Disconnected);
            }
        }
    }

    public override MatchState? OnUpdate()
    {
        return null;
    }
    public override void OnExit() { }
}

public class SharedWarmup : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.Warmup;
    public override void OnEnter()
    {
        IU.GarbageCollectWorldLoot();
    }

    public override MatchState? OnUpdate()
    {
        var remaining = H.Arena.StateTimer;
        var total = H.Arena.PhaseDurationSeconds;
        var elapsed = total - remaining;

        bool allReady = H.Scoreboard.Count > 0;

        if (allReady)
        {
            foreach (var p in H.Scoreboard.Values)
            {
                if (p.ReadyState == PlayerReadinessState.Connected)
                {
                    allReady = false;
                    break;
                }
            }
        }

        // if (allReady) return MatchState.WarmupEnd;

        if (remaining <= 0) return MatchState.WarmupEnd;

        if (elapsed >= 8f && allReady) return MatchState.WarmupEnd;

        return null;
    }

    public override void OnExit()
    {

    }
}

public class SharedWarmupEnd : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.WarmupEnd;
    public override void OnEnter()
    {
        if (!H.IsHeadless)
        {
            UniTask.Void(async () =>
            {
                await UniTask.Delay((int)H.Arena.PhaseDurationSeconds * 1000 - 3000);

                PU.CloseEyes(false, false).Forget();

                await UniTask.Delay(2250);

                H.BetterAudio.FadeMixerVolume(H.BetterAudio.AudioMixerData.InGameVolumeMixer, -80f, 0.75f);

            });
        }

        foreach (var player in H.AllPlayers)
        {
            player.ForceUnlockInventory();
            player.Context?.SetHardReset();
        }
    }

    public override MatchState? OnUpdate() => H.Arena.StateTimer <= 0 ? MatchState.Cleanup : null;
    public override void OnExit()
    {
        H.Session.InitializeScoreBoard();
        H.Arena.economyManager.ResetEconomy();

        if (H.IsServer && H.Gamemode is IGMTeam)
        {
            int ctCount = 0;
            int tCount = 0;

            foreach (var p in H.Scoreboard.Values)
            {
                if (p.ReadyState == PlayerReadinessState.Disconnected)
                    continue;

                if (p.Faction == Faction.CT) ctCount++;
                else if (p.Faction == Faction.T) tCount++;
            }

            bool factionsChanged = false;

            foreach (var p in H.Scoreboard.Values)
            {
                if (p.ReadyState == PlayerReadinessState.Disconnected)
                    continue;

                if (p.Faction != Faction.CT && p.Faction != Faction.T)
                {
                    Faction assignedFaction = ctCount <= tCount ? Faction.CT : Faction.T;
                    p.ChangeFaction(assignedFaction);

                    if (assignedFaction == Faction.CT) ctCount++;
                    else tCount++;

                    factionsChanged = true;
                }
            }

            if (factionsChanged)
            {
                Singleton<SessionManagerSyncPacketWarden>.Instance.Send();
            }
        }
    }
}

public class SharedCleanup : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.Cleanup;
    public override void OnEnter()
    {
        if (!H.IsHeadless) H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();

        int totalRounds = H.Session.factionWins.Values.Sum();
        bool isHalfTime = false;
        if (H.Gamemode is IGMRound roundBased and IGMSideSwappable)
        {
            isHalfTime = totalRounds == roundBased.MaxRoundsToWin - 1;
        }

        // Replenish/Reset Inventories
        foreach (var player in H.AllPlayers)
        {
            try
            {
                player.ForceUnlockInventory();

                if (H.IsServer)
                {
                    Singleton<ReconnectSnapshotterResetPacketWarden>.Instance.Send(player);
                    if (!player.Context.ShouldHardReset && totalRounds > 0 && !isHalfTime)
                    {
                        H.Arena.inventoryManager.Replenish(player);
                    }
                    else
                    {
                        H.Arena.inventoryManager.HardReset(player);
                    }
                }
            }
            catch (Exception ex)
            {
                D.Log($"[SharedCleanup] Error occured during Inventory Management for {player.Profile.Nickname}");
            }
        }

        // if Search and Destroy - add a bomb to a random terrorist
        if (H.IsServer && H.Gamemode is SNDGamemode)
        {
            if (H.Session.GetPlayersFromFaction(Faction.T).Count > 0)
            {
                Player selectedTerrorist = null;

                Player assignedPlayer = Singleton<AskForBombPriorityPacketWarden>.Instance.AssignedPlayer;
                if (assignedPlayer != null)
                    selectedTerrorist = assignedPlayer;
                else
                    selectedTerrorist = H.Session.GetPlayersFromFaction(Faction.T).Where(p => p.Context.ReadyState == PlayerReadinessState.Ready).RandomElement();

                var backpackSlot = selectedTerrorist.Inventory.Equipment.GetSlot(EquipmentSlot.Backpack);

                backpackSlot.RemoveItemWithoutRestrictions();

                Item BombBackpack = IU.CreateItemFromTemplateId(Hardcode.BOMB_BACKPACK);
                backpackSlot.AddWithoutRestrictions(BombBackpack.CloneItem());
            }
        }

        // Broadcast all new equipment
        if (H.IsServer)
        {
            foreach (var player in H.AllPlayers)
            {
                Singleton<EquipmentResyncPacketWarden>.Instance.Send(player, EquipmentResyncRequestType.CleanupBroadcast);
            }
        }

        UniTask.Void(async ct =>
        {
            try
            {
                await UniTask.Delay(250, cancellationToken: MatchStateCancellationToken);

                IU.GarbageCollectWorldLoot();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                if (!H.IsHeadless)
                {
                    await UniTask.Delay(500, cancellationToken: MatchStateCancellationToken);

                    HU.HealMe().Forget();
                    if (H.MainPlayer.MovementContext.IsInPronePose) H.MainPlayer.MovementContext.IsInPronePose = false;

                    H.MainPlayer.MovementContext.SetPoseLevel(1f, false);
                    await UniTask.Delay(750, cancellationToken: MatchStateCancellationToken);
                    Teleporter.Teleport(H.MainPlayer, H.Session.level, H.MainPlayerScore.Faction);
                }
            }
            catch (OperationCanceledException) { }
        }, cancellationToken: MatchStateCancellationToken);
    }
    public override MatchState? OnUpdate() => H.Arena.StateTimer <= 0 ? MatchState.RoundPrepare : null;
    public override void OnExit() { }
}

public class SharedPause : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.Pause;
    public override void OnEnter() { }
    public override MatchState? OnUpdate() => H.Arena.StateTimer <= 0 ? MatchState.RoundPrepare : null;
    public override void OnExit() { }
}

public class SharedPrepare : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.RoundPrepare;
    public override void OnEnter()
    {
        if (!H.IsHeadless)
        {
            PU.OpenEyes();
            H.BetterAudio.FadeMixerVolume(H.BetterAudio.AudioMixerData.InGameVolumeMixer, 0f, 0.5f);

            // H.MainPlayer.SetFirstAvailableItem((result) => { });

            if (H.MainPlayer.GetSlotItem(EquipmentSlot.Backpack) != null)
                D.Notify("You have the bomb.");
        }

        foreach (var player in H.AllPlayingPlayers)
        {
            player.Context.Spawn();
        }
    }

    public override MatchState? OnUpdate() => H.IsServer && H.Arena.StateTimer <= 0 ? MatchState.RoundAction : null;

    public override void OnExit()
    {

    }
}

public class GenericTeamRoundBasedAction : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.RoundAction;
    public override void OnEnter() { }
    public override MatchState? OnUpdate()
    {
        Faction? winner = CheckWipe();
        if (winner.HasValue)
        {
            H.Arena.Award(winner.Value, RoundWinReason.Elimination);
            return MatchState.RoundEnd;
        }

        if (H.Arena.StateTimer <= 0)
        {
            Faction randomWinner = UnityEngine.Random.Range(0, 2) == 0 ? Faction.CT : Faction.T;
            H.Arena.Award(randomWinner, RoundWinReason.Timeout);
            return MatchState.RoundEnd;
        }

        return null;
    }
    public override void OnExit() { }

    private Faction? CheckWipe()
    {
        int aliveCT = 0;
        int aliveT = 0;
        foreach (var p in H.Scoreboard.Values)
        {
            if (p.IsAlive && p.Faction == Faction.CT) aliveCT++;
            if (p.IsAlive && p.Faction == Faction.T) aliveT++;
        }

        if (aliveCT == 0) return Faction.T;
        if (aliveT == 0) return Faction.CT;
        return null;
    }
}

public class SharedRoundEnd : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.RoundEnd;
    public override void OnEnter()
    {
        if (H.IsServer)
        {
            Singleton<SessionManagerSyncPacketWarden>.Instance.Send();
        }

        if (!H.IsHeadless)
        {
            UniTask.Void(async ct =>
            {
                try
                {
                    await UniTask.Delay((int)H.Gamemode.StateTimerConfig[StateType] * 1000 - 3000, cancellationToken: MatchStateCancellationToken);

                    PU.CloseEyes(false, false).Forget();

                    await UniTask.Delay(2250, cancellationToken: MatchStateCancellationToken);

                    H.BetterAudio.FadeMixerVolume(H.BetterAudio.AudioMixerData.InGameVolumeMixer, -80f, 0.75F);
                }
                catch (OperationCanceledException) { }
            }, cancellationToken: MatchStateCancellationToken);
        }
    }

    public override MatchState? OnUpdate()
    {
        if (H.Arena.StateTimer <= 0)
        {
            if (H.Gamemode is IGMRound roundBasedGamemode)
            {
                var wins = H.Session.factionWins;

                if (H.Gamemode is IGMSideSwappable)
                {
                    if (wins[Faction.CT] + wins[Faction.T] == roundBasedGamemode.MaxRoundsToWin - 1)
                    {
                        return MatchState.SideSwap;
                    }
                }

                if (wins[Faction.CT] >= roundBasedGamemode.MaxRoundsToWin || wins[Faction.T] >= roundBasedGamemode.MaxRoundsToWin)
                {
                    return MatchState.MatchEnd;
                }
            }
            return MatchState.Cleanup;
        }

        return null;
    }
    public override void OnExit()
    {
        IU.GarbageCollectWorldLoot();
        H.RagdollCreator.ClearAllCorpses();
        H.Session.ResetRoundScopeFields();
    }
}

public class SharedSideSwap : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.SideSwap;

    public override void OnEnter()
    {
        if (H.Gamemode is IGMSideSwappable sideSwappable)
        {
            if (H.Gamemode is IGMBuyable)
            {
                H.Arena.economyManager.ResetEconomy();
            }
            foreach (var player in H.AllPlayers)
            {
                var playerScore = H.GetPlayerContext(player.Id);
                var swappedFaction = playerScore.Faction == Faction.CT ? Faction.T : Faction.CT;
                playerScore.ChangeFaction(swappedFaction);
            }
            (H.Session.factionWins[Faction.CT], H.Session.factionWins[Faction.T]) = (H.Session.factionWins[Faction.T], H.Session.factionWins[Faction.CT]);
            Singleton<SessionManagerSyncPacketWarden>.Instance.Send();
        }
    }

    public override MatchState? OnUpdate() => H.IsServer && H.Arena.StateTimer <= 0 ? MatchState.Cleanup : null;

    public override void OnExit()
    {
        (H.Gamemode as IGMSideSwappable).HasSideSwapped = true;
    }
}

// we go back to none and lobby here
public class SharedMatchEnd : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.MatchEnd;
    public override void OnEnter() { }
    public override MatchState? OnUpdate() => H.Arena.StateTimer <= 0 ? MatchState.None : null;
    public override void OnExit() { }
}