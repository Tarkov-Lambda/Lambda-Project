using arena.ui;
using EFT;
using System;

namespace ifp.arena.bep.Core.UI
{
    internal class SpectatorController : IDisposable
    {
        readonly ArenaMatchUI matchUI;

        internal SpectatorController(ArenaMatchUI matchUI)
        {
            this.matchUI = matchUI;

            SpectatorManager.OnSelfStartSpectating += OnStartSpectating;
            SpectatorManager.OnSelfStopSpectating += OnStopSpectating;

            matchUI.Spectator.gameObject.SetActive(false);
        }

        private void OnStartSpectating(Player player)
        {
            PlayerScore playerScore = H.GetPlayerScore(player);
            matchUI.Spectator.SetSpectatingPlayer(playerScore.Score);
            matchUI.Spectator.gameObject.SetActive(true);
        }

        private void OnStopSpectating()
        {
            matchUI.Spectator.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            SpectatorManager.OnSelfStartSpectating -= OnStartSpectating;
            SpectatorManager.OnSelfStopSpectating -= OnStopSpectating;
        }
    }
}
