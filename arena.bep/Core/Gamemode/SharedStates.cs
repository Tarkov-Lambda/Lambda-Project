using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.UI;
using Fika.Core;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.networking;
using ifp.arena.shared.Models;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.Core.Gamemode;

// just kind of a "nothing is happening" state type beat
public class SharedNone : IGameState
{
    public MatchState StateType => MatchState.None;
    public virtual void OnEnter()
    {
        Teleporter.Teleport(H.MainPlayer, "lobby", Faction.None);
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
        if (!H.IsServer) return null;

        var remaining = H.Arena.StateTimer;
        var total = H.ActiveRules.StateTimerConfig[MatchState.Warmup];
        var elapsed = total - remaining;

        bool allReady = H.Scoreboard.Count > 0 && H.Scoreboard.Values.All(p => p.ReadyState != PlayerReadinessState.Connected);

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
                await UniTask.Delay((int)H.ActiveRules.StateTimerConfig[StateType] * 1000 - 1500);
                PU.CloseEyes(false, false).Forget();
            });
        }

        // foreach (var player in H.AllPlayers)
        // {
        //     player.ForceUnlockInventory();
        // }
    }

    public virtual MatchState? OnUpdate() => H.Arena.StateTimer <= 0 ? MatchState.Cleanup : null;
    public virtual void OnExit()
    {
        H.Session.InitializeScoreBoard();
        H.Arena.economyManager.ResetEconomy();

        if (!H.IsHeadless)
        {
            // players start the match alive and so we have to invoke this here instead of relying on cleanup
            // kinda dumb?
            InventoryResetter.HardReset().Forget();
        }
    }
}

public class SharedCleanup : IGameState
{
    public MatchState StateType => MatchState.WarmupEnd;
    public virtual void OnEnter()
    {
        IU.GarbageCollectWorldLoot();

        // if (H.IsServer)
        // {
        //     foreach (var playerScore in H.Scoreboard)
        //     {
        //         Vector3 newPos = playerScore.Value.GetBestSpawnPoint();
        //         Singleton<DictateTeleportHandler>.Instance.SendToPlayer(playerScore.Value.player, newPos);
        //     }
        // }

        if (!H.IsHeadless)
        {
            H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();

            UniTask.Void(async () =>
            {
                await UniTask.Delay(750);

                HU.HealMe().Forget();
                HU.ResetObservedPlayersHealth();

                // await InventoryResetter.HardReset();

                if (H.MainPlayerScore.IsAlive)
                {
                    await InventoryResetter.SoftReset();
                }
                else
                {
                    await InventoryResetter.HardReset();
                }
            });
        }
    }
    public virtual MatchState? OnUpdate() => H.Arena.StateTimer <= 0 ? MatchState.RoundPrepare : null;
    public virtual void OnExit()
    {
    }
}

public class SharedPause : IGameState
{
    public MatchState StateType => MatchState.Pause;
    public virtual void OnEnter() { }

    public virtual MatchState? OnUpdate()
    {
        if (!H.IsServer) return null;
        if (H.Arena.StateTimer <= 0) return MatchState.RoundPrepare;
        return null;
    }
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
            Teleporter.Teleport(H.MainPlayer, H.Session.mapName, H.MainPlayerScore.Faction);

            PU.OpenEyes();
        }

        foreach (var p in H.Arena.session.scoreboard.Values)
        {
            p.Spawn();
        }
    }

    public virtual MatchState? OnUpdate() => H.IsServer && H.Arena.StateTimer <= 0 ? MatchState.RoundAction : null;

    public virtual void OnExit()
    {
        if (H.ActiveRules != null && H.ActiveRules is SND_ModeRules)
        {
            Singleton<BombAssignmentPacketHandler>.Instance.Send();
        }
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
                await UniTask.Delay((int)H.ActiveRules.StateTimerConfig[StateType] * 1000 - 1500);
                PU.CloseEyes(false, false).Forget();
            });
        }
    }

    public virtual MatchState? OnUpdate()
    {
        if (H.IsClient) return null;

        if (H.Arena.StateTimer <= 0)
        {
            if (H.ActiveRules is IGMRound roundBasedGamemode)
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
        H.Arena.economyManager.ResetEconomy();

        if (H.ActiveRules is IGMSideSwappable sideSwappable)
        {
            foreach (var player in H.AllPlayers)
            {
                var playerScore = H.GetPlayerScore(player.Id);
                var swappedFaction = playerScore.Faction == Faction.CT ? Faction.T : Faction.CT;
                playerScore.ChangeFaction(swappedFaction);
            }
            (H.Session.factionWins[Faction.CT], H.Session.factionWins[Faction.T]) = (H.Session.factionWins[Faction.T], H.Session.factionWins[Faction.CT]);
            Singleton<SessionManagerSyncPacketHandler>.Instance.Send();
        }


        if (!H.IsHeadless)
        {
            InventoryResetter.HardReset().Forget();
        }
    }
    public virtual MatchState? OnUpdate() => H.IsServer && H.Arena.StateTimer <= 0 ? MatchState.Cleanup : null;
    public virtual void OnExit()
    {
        (H.ActiveRules as IGMSideSwappable).HasSideSwapped = true;
    }
}

// Really only used for UI and actions so doesn't really matter ig
public class SharedFinish : IGameState
{
    public MatchState StateType => MatchState.MatchEnd;
    public virtual void OnEnter() { }
    public virtual MatchState? OnUpdate() => null;
    public virtual void OnExit() { }
}
