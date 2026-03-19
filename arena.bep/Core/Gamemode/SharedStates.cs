using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
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
        public void OnEnter() { }
        public MatchState? OnUpdate()
        {
            return null;
        }
        public void OnExit() { }
    }

    public class SharedWarmup : IGameState
    {
        public MatchState StateType => MatchState.Warmup;
        public void OnEnter()
        {
            foreach (var p in H.Arena.session.scoreboard.Values)
            {
                p.SetMoney(EconomyConstants.MAX_MONEY);
            }
        }

        public MatchState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (H.Arena.StateTimer <= 0 || H.Scoreboard.Count > 0 && H.Scoreboard.Values.All(p => p.isMapReady))
                return MatchState.WarmupEnd;
            return null;
        }
        public void OnExit()
        {


        }
    }

    public class SharedWarmupEnd : IGameState
    {
        public MatchState StateType => MatchState.WarmupEnd;
        public void OnEnter()
        {

        }
        public MatchState? OnUpdate() => FikaBackendUtils.IsServer && H.Arena.StateTimer <= 0 ? MatchState.RoundPrepare : null;
        public void OnExit()
        {
            H.Session.InitializeScoreBoard();
            InventoryResetter.ResetInventory();
            H.Session.ResetRoundScopeFields();
        }
    }

    public class SharedPause : IGameState
    {
        public MatchState StateType => MatchState.Warmup;
        public void OnEnter() { }

        public MatchState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (H.Arena.StateTimer <= 0) return MatchState.RoundPrepare;
            return null;
        }
        public void OnExit() { }
    }

    public class SharedPrepare : IGameState
    {
        public MatchState StateType => MatchState.RoundPrepare;
        public void OnEnter()
        {
            if (!H.MainPlayerScore.isAlive)
            {
                InventoryResetter.ResetInventory();
                PlayerUtils.OpenEyes();
            }
            
            foreach (var p in H.Arena.session.scoreboard.Values)
            {
                p.Spawn();
            }

            if (H.GameWorld?.MainPlayer != null)
            {
                Teleporter.Teleport(H.GameWorld.MainPlayer);
                PlayerUtils.FixMe();
            }

            H.Session.bombState = BombState.None;

            H.Arena.LastObjectivePlayerId = -1;
            H.Arena.LastObjectiveBombState = BombState.None;

            // Hide any leftover bomb visual from the previous round
            Singleton<ArenaController>.Instance.SetBombVisuals(new BombStatePacket { state = BombState.None });

            ItemsUtils.TryRemoveSlot(EquipmentSlot.Backpack, H.MainPlayer, true).Forget();
        }

        public MatchState? OnUpdate() => FikaBackendUtils.IsServer && H.Arena.StateTimer <= 0 ? MatchState.RoundAction : null;

        public void OnExit()
        {
            if (H.Arena.ActiveRules != null && H.Arena.ActiveRules is SnDModeRules)
            {
                Singleton<BombAssignmentPacketHandler>.Instance.SendDelayed().Forget();
            }
            // int currentRound = H.Session.factionWins.Values.Sum();
            // int maxRounds = SnDModeRules.maxRoundsToWin * 2 - 1;
            // double minutes = TimeOfDayHelper.GetMinutesForRound(currentRound, maxRounds);
            // Singleton<WeatherAndTimePacketHandler>.Instance.Send((int)minutes);
        }
    }

    public class SharedEnd : IGameState
    {
        public MatchState StateType => MatchState.RoundEnd;
        public void OnEnter()
        {
            if (FikaBackendUtils.IsServer)
            {
                H.Arena.OnRoundEnd();
            }
        }
        public MatchState? OnUpdate()
        {
            if (FikaBackendUtils.IsClient) return null;

            if (H.Arena.StateTimer <= 0)
            {
                if (H.Arena.ActiveRules is SnDModeRules)
                {
                    SnDModeRules snd = H.Arena.ActiveRules as SnDModeRules;
                    var wins = H.Session.factionWins;

                    if (wins[Faction.CT] + wins[Faction.T] == SnDModeRules.maxRoundsToWin - 1)
                    {
                        return MatchState.SideSwap;
                    }

                    if (wins[Faction.CT] >= SnDModeRules.maxRoundsToWin || wins[Faction.T] >= SnDModeRules.maxRoundsToWin)
                    {
                        return MatchState.MatchEnd;
                    }
                }
                return MatchState.RoundPrepare;
            }

            return null;
        }
        public void OnExit()
        {
        }
    }

    public class SharedSideSwap : IGameState
    {
        public MatchState StateType => MatchState.SideSwap;
        public void OnEnter()
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
        public MatchState? OnUpdate() => FikaBackendUtils.IsServer && H.Arena.StateTimer <= 0 ? MatchState.RoundPrepare : null;
        public void OnExit() { }
    }

    // Really only used for UI and actions so doesn't really matter ig
    public class SharedFinish : IGameState
    {
        public MatchState StateType => MatchState.MatchEnd;
        public void OnEnter() { }
        public MatchState? OnUpdate() => null;
        public void OnExit() { }

    }
}
