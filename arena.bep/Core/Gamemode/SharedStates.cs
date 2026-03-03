using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using ifp.arena.bep.Core.Dying;
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
        public void OnEnter() { Teleporter.Teleport(Singleton<GameWorld>.Instance.MainPlayer); }
        public MatchState? OnUpdate()
        {
            return null;
        }
        public void OnExit() { }
    }

    // Sets when server says we are restarting
    public class SharedWarmup : IGameState
    {
        public MatchState StateType => MatchState.Warmup;
        public void OnEnter()
        {
            if (FikaBackendUtils.IsServer) H.game.StateTimer = 45f;
        }

        public MatchState? OnUpdate()
        {
            if (!FikaBackendUtils.IsServer) return null;
            if (H.game.StateTimer <= 0 || H.scoreboard.Count > 0 && H.scoreboard.Values.All(p => p.isReady))
                return MatchState.WarmupEnd;
            return null;
        }
        public void OnExit() { }
    }

    // Triggers whenever a minimum warmup time has been reached and players have been loaded, or warmup full time has ended
    public class SharedWarmupEnd : IGameState
    {
        public MatchState StateType => MatchState.WarmupEnd;
        public void OnEnter() { if (FikaBackendUtils.IsServer) H.game.StateTimer = 5f; }
        public MatchState? OnUpdate() => FikaBackendUtils.IsServer && H.game.StateTimer <= 0 ? MatchState.RoundPrepare : null;
        public void OnExit() { }
    }

    public class SharedPrepare : IGameState
    {
        public MatchState StateType => MatchState.RoundPrepare;
        public void OnEnter()
        {
            foreach (var p in H.game.session.scoreboard.Values) p.isAlive = true;
            if (Singleton<GameWorld>.Instance?.MainPlayer != null)
            {
                Teleporter.Teleport(Singleton<GameWorld>.Instance.MainPlayer);
                Patch_Kill.FixMe(Singleton<GameWorld>.Instance.MainPlayer.ActiveHealthController);
            }

            if (FikaBackendUtils.IsServer) H.game.StateTimer = 5f;
            H.game.session.scoreboard.Clear();
            H.game.session.InitializeScoreBoard();
        }
        public MatchState? OnUpdate() => FikaBackendUtils.IsServer && H.game.StateTimer <= 0 ? MatchState.RoundAction : null;
        public void OnExit() { }
    }

    public class SharedEnd : IGameState
    {
        public MatchState StateType => MatchState.RoundEnd;
        public void OnEnter()
        {
            if (FikaBackendUtils.IsServer)
            {
                H.game.StateTimer = 10f;
                H.game.OnRoundEnd();
            }
        }
        public MatchState? OnUpdate()
        {
            if (FikaBackendUtils.IsClient) return null;

            if (H.game.StateTimer <= 0)
            {
                if (H.game.ActiveRules is SnDModeRules)
                {
                    SnDModeRules snd = H.game.ActiveRules as SnDModeRules;
                    var wins = H.session.factionWins;

                    if (wins[Faction.CT] + wins[Faction.T] == snd.maxRoundsToWin - 1)
                    {
                        return MatchState.SideSwap;
                    }

                    if (wins[Faction.CT] >= snd.maxRoundsToWin || wins[Faction.T] >= snd.maxRoundsToWin)
                    {
                        return MatchState.MatchEnd;
                    }
                }
                return MatchState.RoundPrepare;
            }

            return null;
        }
        public void OnExit() { }
    }

    public class SharedSideSwap : IGameState
    {
        public MatchState StateType => MatchState.SideSwap;
        public void OnEnter()
        {
            if (FikaBackendUtils.IsServer)
            {
                H.game.StateTimer = 10f;
                H.game.OnRoundEnd();
                foreach (var player in H.GetAllPlayers())
                {
                    var playerScore = H.GetPlayerScore(player.Id);
                    playerScore.faction = playerScore.faction == Faction.CT ? Faction.T : Faction.CT;
                }
                (H.session.factionWins[Faction.CT], H.session.factionWins[Faction.T]) = (H.session.factionWins[Faction.T], H.session.factionWins[Faction.CT]);
                Singleton<SessionInfoPacketHandler>.Instance.Send();
            }
        }
        public MatchState? OnUpdate() => FikaBackendUtils.IsServer && H.game.StateTimer <= 0 ? MatchState.RoundPrepare : null;
        public void OnExit() { }
    }

    // Really only used for UI and actions so doesn't really matter ig
    public class SharedFinish : SharedNone
    {
        new public MatchState StateType => MatchState.MatchEnd;
    }
}
