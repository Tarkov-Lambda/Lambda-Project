using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core;
using Fika.Core.Main.Utils;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using System.Linq;

namespace ifp.arena.bep.Core.Gamemode
{
    // just kind of a "nothing is happening" state type beat
    public class SharedNone : IGameState
    {
        public MatchState StateType => MatchState.None;
        public virtual void OnEnter() { }
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
            foreach (var p in H.Arena.session.scoreboard.Values)
            {
                p.SetMoney(EconomyConstants.MAX_MONEY);
            }
            IU.GarbageCollectWorldLoot();
        }

        public virtual MatchState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (H.Arena.StateTimer <= 0 || H.Scoreboard.Count > 0 && H.Scoreboard.Values.All(p => p.isMapReady))
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

        }
        public virtual MatchState? OnUpdate() => FikaBackendUtils.IsServer && H.Arena.StateTimer <= 0 ? MatchState.RoundPrepare : null;
        public virtual void OnExit()
        {
            H.Session.InitializeScoreBoard();
            InventoryResetter.ResetInventory().Forget();
            H.Session.ResetSessionScopeFields(); // full reset
        }
    }

    public class SharedPause : IGameState
    {
        public MatchState StateType => MatchState.Warmup;
        public virtual void OnEnter() { }

        public virtual MatchState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
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
            H.Session.ResetRoundScopeFields(); // I've lost the plot and I have no clue how to sync states correctly anymore
            IU.GarbageCollectWorldLoot();

            // H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();

            if (!H.MainPlayerScore.isAlive)
            {
                InventoryResetter.ResetInventory().Forget();
                PU.OpenEyes();
            }

            foreach (var p in H.Arena.session.scoreboard.Values)
            {
                p.Spawn();
            }

            Teleporter.Teleport(H.MainPlayer, H.Session.mapName, H.MainPlayerScore.faction);
            HU.HealMe().Forget();



            HU.ResetObservedPlayersHealth();

            H.SpectatorManager.StopSpectating();
        }

        public virtual MatchState? OnUpdate() => FikaBackendUtils.IsServer && H.Arena.StateTimer <= 0 ? MatchState.RoundAction : null;

        public virtual void OnExit()
        {
            if (H.Arena.ActiveRules != null && H.Arena.ActiveRules is SND_ModeRules)
            {
                Singleton<BombAssignmentPacketHandler>.Instance.SendDelayed().Forget();
            }

        }
    }

    public class SharedEnd : IGameState
    {
        public MatchState StateType => MatchState.RoundEnd;
        public virtual void OnEnter()
        {
            if (FikaBackendUtils.IsServer)
            {

                H.Arena.OnRoundEnd();
            }
        }
        public virtual MatchState? OnUpdate()
        {
            if (FikaBackendUtils.IsClient) return null;

            if (H.Arena.StateTimer <= 0)
            {
                if (H.Arena.ActiveRules is SND_ModeRules)
                {
                    SND_ModeRules snd = H.Arena.ActiveRules as SND_ModeRules;
                    var wins = H.Session.factionWins;

                    if (wins[Faction.CT] + wins[Faction.T] == SND_ModeRules.maxRoundsToWin - 1)
                    {
                        return MatchState.SideSwap;
                    }

                    if (wins[Faction.CT] >= SND_ModeRules.maxRoundsToWin || wins[Faction.T] >= SND_ModeRules.maxRoundsToWin)
                    {
                        return MatchState.MatchEnd;
                    }
                }
                return MatchState.RoundPrepare;
            }

            return null;
        }
        public virtual void OnExit()
        {
            IU.GarbageCollectWorldLoot();
            Singleton<RagdollCreator>.Instance.ClearAllCorpses();
            H.BombHandler.bombVisuals?.SetActive(false);
        }
    }

    public class SharedSideSwap : IGameState
    {
        public MatchState StateType => MatchState.SideSwap;
        public virtual void OnEnter()
        {
            if (FikaBackendUtils.IsServer)
            {
                foreach (var player in H.AllPlayers)
                {
                    var playerScore = H.GetPlayerScore(player.Id);
                    playerScore.faction = playerScore.faction == Faction.CT ? Faction.T : Faction.CT;
                }
                (H.Session.factionWins[Faction.CT], H.Session.factionWins[Faction.T]) = (H.Session.factionWins[Faction.T], H.Session.factionWins[Faction.CT]);
                Singleton<SessionInfoPacketHandler>.Instance.Send();
            }
        }
        public virtual MatchState? OnUpdate() => FikaBackendUtils.IsServer && H.Arena.StateTimer <= 0 ? MatchState.RoundPrepare : null;
        public virtual void OnExit() { }
    }

    // Really only used for UI and actions so doesn't really matter ig
    public class SharedFinish : IGameState
    {
        public MatchState StateType => MatchState.MatchEnd;
        public virtual void OnEnter() { }
        public virtual MatchState? OnUpdate() => null;
        public virtual void OnExit() { }

    }
}
