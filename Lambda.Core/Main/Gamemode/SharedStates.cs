using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core;
using Lambda.Core.Main.Dying;
using Lambda.Core.Networking;
using System.Linq;

namespace Lambda.Core.Main.Gamemode;

// just kind of a "nothing is happening" state type beat
public class SharedNone : IGameState
{
    public MatchState StateType => MatchState.None;
    public virtual void OnEnter()
    {
        if (!H.IsHeadless)
        {
            Teleporter.Teleport(H.MainPlayer, "lobby", Faction.None);
            H.BetterAudio.FadeMixerVolume(H.BetterAudio.AudioMixerData.InGameVolumeMixer, 0f, 1f);
            PU.OpenEyes();
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

    public virtual MatchState? OnUpdate()
    {
        return null;
    }
    public virtual void OnExit() { }
}

public class SharedWarmup : IGameState
{
    public MatchState StateType => MatchState.Warmup;
    public virtual void OnEnter()
    {
        IU.GarbageCollectWorldLoot();
    }

    public virtual MatchState? OnUpdate()
    {
        var remaining = H.Arena.StateTimer;
        var total = H.Gamemode.StateTimerConfig[StateType];
        var elapsed = total - remaining;

        bool allReady = H.Scoreboard.Count > 0 && H.Scoreboard.Values.All(p => p.ReadyState != PlayerReadinessState.Connected);

        if (allReady) return MatchState.WarmupEnd;
        if (remaining <= 0)
            return MatchState.WarmupEnd;

        if (elapsed >= 15f && allReady)
            return MatchState.WarmupEnd;

        return null;
    }

    public virtual void OnExit()
    {

    }
}

public class SharedWarmupEnd : IGameState
{
    public MatchState StateType => MatchState.WarmupEnd;
    public virtual void OnEnter()
    {
        if (!H.IsHeadless)
        {
            UniTask.Void(async () =>
            {
                await UniTask.Delay((int)H.Gamemode.StateTimerConfig[StateType] * 1000 - 3000);

                PU.CloseEyes(false, false).Forget();

                await UniTask.Delay(2250);

                H.BetterAudio.FadeMixerVolume(H.BetterAudio.AudioMixerData.InGameVolumeMixer, -80f, 0.75f);

            });
        }

        foreach (var player in H.AllPlayers)
        {
            player.ForceUnlockInventory();
            player.GetContext()?.SetHardReset();
        }
    }

    public virtual MatchState? OnUpdate() => H.Arena.StateTimer <= 0 ? MatchState.Cleanup : null;
    public virtual void OnExit()
    {
        H.Session.InitializeScoreBoard();
        H.Arena.economyManager.ResetEconomy();

    }
}

public class SharedCleanup : IGameState
{
    public MatchState StateType => MatchState.Cleanup;
    public virtual void OnEnter()
    {
        // if (!H.IsHeadless)
        // {
        //     H.MainPlayer.SetEmptyHands(delegate { });
        // }

        IU.GarbageCollectWorldLoot();

        int totalRounds = H.Session.factionWins.Values.Sum();
        bool isHalfTime = false;
        if (H.Gamemode is IGMRound roundBased and IGMSideSwappable)
        {
            isHalfTime = totalRounds == roundBased.MaxRoundsToWin - 1;
        }

        // Replenish/Reset Inventories
        foreach (var player in H.AllPlayers)
        {
            player.ForceUnlockInventory();

            if (H.IsServer)
            {
                Singleton<ReconnectSnapshotterResetPacketWarden>.Instance.Send(player);

                var pContext = H.GetPlayerContext(player);

                if (!pContext.ShouldHardReset && totalRounds > 0 && !isHalfTime)
                {
                    InventoryManager.Replenish(player);
                }
                else
                {
                    InventoryManager.HardReset(player);
                }
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
                    selectedTerrorist = H.Session.GetPlayersFromFaction(Faction.T).Where(p => p.GetContext().ReadyState == PlayerReadinessState.Ready).RandomElement();

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
                Singleton<EquipmentResyncPacketWarden>.Instance.Send(player, true);
            }
        }

        if (!H.IsHeadless)
        {
            H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();

            UniTask.Void(async () =>
            {
                await UniTask.Delay(750);

                HU.HealMe().Forget();
                if (H.MainPlayer.MovementContext.IsInPronePose)
                {
                    H.MainPlayer.MovementContext.IsInPronePose = false;
                }

                H.MainPlayer.MovementContext.SetPoseLevel(1f, false);
                await UniTask.Delay(750);
                Teleporter.Teleport(H.MainPlayer, H.Session.level, H.MainPlayerScore.Faction);
            });
        }
    }
    public virtual MatchState? OnUpdate() => H.Arena.StateTimer <= 0 ? MatchState.RoundPrepare : null;
    public virtual void OnExit() { }
}

public class SharedPause : IGameState
{
    public MatchState StateType => MatchState.Pause;
    public virtual void OnEnter() { }
    public virtual MatchState? OnUpdate() => H.Arena.StateTimer <= 0 ? MatchState.RoundPrepare : null;
    public virtual void OnExit() { }
}

public class SharedPrepare : IGameState
{
    public MatchState StateType => MatchState.RoundPrepare;
    public virtual void OnEnter()
    {
        H.Session.ResetRoundScopeFields();

        if (!H.IsHeadless)
        {
            Teleporter.Teleport(H.MainPlayer, H.Session.level, H.MainPlayerScore.Faction);

            PU.OpenEyes();

            H.BetterAudio.FadeMixerVolume(H.BetterAudio.AudioMixerData.InGameVolumeMixer, 0f, 0.5f);

            // H.MainPlayer.SetFirstAvailableItem((result) => { });
        }

        foreach (var player in H.AllPlayingPlayers)
        {
            player.GetContext().Spawn();
        }
    }

    public virtual MatchState? OnUpdate() => H.IsServer && H.Arena.StateTimer <= 0 ? MatchState.RoundAction : null;

    public virtual void OnExit()
    {

    }
}

public class SharedRoundEnd : IGameState
{
    public MatchState StateType => MatchState.RoundEnd;
    public virtual void OnEnter()
    {
        if (H.IsServer)
        {
            H.Arena.OnRoundEnd();
        }

        if (!H.IsHeadless)
        {
            UniTask.Void(async () =>
            {
                await UniTask.Delay((int)H.Gamemode.StateTimerConfig[StateType] * 1000 - 3000);

                PU.CloseEyes(false, false).Forget();

                await UniTask.Delay(2250);

                H.BetterAudio.FadeMixerVolume(H.BetterAudio.AudioMixerData.InGameVolumeMixer, -80f, 0.75F);
            });
        }
    }

    public virtual MatchState? OnUpdate()
    {
        if (H.IsClient) return null;

        if (H.Arena.StateTimer <= 0)
        {
            if (H.Gamemode is IGMRound roundBasedGamemode)
            {
                var wins = H.Session.factionWins;

                if (wins[Faction.CT] + wins[Faction.T] == roundBasedGamemode.MaxRoundsToWin - 1)
                {
                    return MatchState.SideSwap;
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
    public virtual void OnExit()
    {
        IU.GarbageCollectWorldLoot();
        H.RagdollCreator.ClearAllCorpses();
    }
}

public class SharedSideSwap : IGameState
{
    public MatchState StateType => MatchState.SideSwap;

    public virtual void OnEnter()
    {
        if (H.Gamemode is IGMSideSwappable sideSwappable)
        {
            if (H.Gamemode is IGMBuyable)
            {
                H.Arena.economyManager.ResetEconomy();
            }
            foreach (var player in H.AllPlayers)
            {
                var playerScore = H.GetPlayerScore(player.Id);
                var swappedFaction = playerScore.Faction == Faction.CT ? Faction.T : Faction.CT;
                playerScore.ChangeFaction(swappedFaction);
            }
            (H.Session.factionWins[Faction.CT], H.Session.factionWins[Faction.T]) = (H.Session.factionWins[Faction.T], H.Session.factionWins[Faction.CT]);
            Singleton<SessionManagerSyncPacketWarden>.Instance.Send();
        }
    }

    public virtual MatchState? OnUpdate() => H.IsServer && H.Arena.StateTimer <= 0 ? MatchState.Cleanup : null;

    public virtual void OnExit()
    {
        (H.Gamemode as IGMSideSwappable).HasSideSwapped = true;
    }
}

// we go back to none and lobby here
public class SharedMatchEnd : IGameState
{
    public MatchState StateType => MatchState.MatchEnd;
    public virtual void OnEnter() { }
    public virtual MatchState? OnUpdate() => H.Arena.StateTimer <= 0 ? MatchState.None : null;
    public virtual void OnExit() { }
}