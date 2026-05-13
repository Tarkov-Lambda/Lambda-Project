using Lambda.UI;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using Lambda.Shared.Models;
using System;
using System.Linq;

namespace Lambda.Core.Main.UI
{
    internal class TopBarController : IDisposable
    {
        readonly TopBar topBar;

        internal TopBarController(TopBar topBar)
        {
            this.topBar = topBar;

            PlayerKilledPacketWarden.AfterPacketApplied += OnPlayerKill;
            PlayerReadinessPacketWarden.AfterPacketApplied += OnPlayerReadiness;
            EventBus.OnEnter += OnMatchStateEnter;
            UnityTicker.OnUpdate += OnUpdate;

        }

        private void OnPlayerKill(PlayerKilledPacket packet) => Refresh();
        private void OnPlayerReadiness(PlayerReadinessPacket packet) => Refresh();
        private void OnMatchStateEnter(MatchState state)
        {
            if (state
            is MatchState.Cleanup
            or MatchState.SideSwap
            or MatchState.RoundPlanted
            or MatchState.RoundEnd
            or MatchState.MatchEnd)
            {
                topBar.ToggleTimer(false);
            }
            else
            {
                topBar.ToggleTimer(true);
            }

            Refresh();
        }

        void Refresh()
        {
            int scoreCT = H.Session.factionWins[Faction.CT];
            int scoreT = H.Session.factionWins[Faction.T];

            topBar.SetScores(scoreCT, scoreT);

            PlayerScoreInfo[] allPlayerStats = H.Scoreboard.Values.Select(p => p.Score).ToArray();

            PlayerScoreInfo[] teamT = allPlayerStats.Where(p => p.Faction == Faction.T).ToArray();
            PlayerScoreInfo[] teamCT = allPlayerStats.Where(p => p.Faction == Faction.CT).ToArray();

            topBar.SetTeamStatuses(teamCT, teamT);
        }

        private void OnUpdate()
        {
            topBar.SetTime(H.Arena.StateTimer);
        }

        public void Dispose()
        {
            PlayerKilledPacketWarden.AfterPacketApplied -= OnPlayerKill;
            PlayerReadinessPacketWarden.AfterPacketApplied -= OnPlayerReadiness;
            EventBus.OnEnter -= OnMatchStateEnter;
            UnityTicker.OnUpdate -= OnUpdate;
        }
    }
}
