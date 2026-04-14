using arena.ui;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using ifp.arena.shared.Models;
using System;
using System.Linq;

namespace ifp.arena.bep.Core.UI
{
    internal class TopBarController : IDisposable
    {
        readonly TopBar topBar;

        internal TopBarController(TopBar topBar)
        {
            EventBus.OnPlayerKill += OnPlayerKill;
            EventBus.OnEnter += OnMatchStateEnter;
            UnityTicker.OnUpdate += OnUpdate;

            this.topBar = topBar;
        }

        private void OnPlayerKill(PlayerKilledPacket packet) => Refresh();
        private void OnMatchStateEnter(MatchState state) => Refresh();

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
            EventBus.OnPlayerKill -= OnPlayerKill;
            EventBus.OnEnter -= OnMatchStateEnter;
            UnityTicker.OnUpdate -= OnUpdate;
        }
    }
}
