using arena.ui;
using EFT;
using System;

namespace ifp.arena.bep.Core.UI
{
    internal class SpectatorController : IDisposable
    {
        readonly Spectator spectator;

        internal SpectatorController(Spectator spectator)
        {
            this.spectator = spectator;

            SpectatorManager.OnSelfStartSpectating += OnStartSpectating;
            SpectatorManager.OnSelfStopSpectating += OnStopSpectating;

            spectator.gameObject.SetActive(false);
        }

        private void OnStartSpectating(Player player)
        {
            PlayerScore playerScore = H.GetPlayerScore(player);
            spectator.SetSpectatingPlayer(playerScore.Score);
            spectator.gameObject.SetActive(true);
        }

        private void OnStopSpectating()
        {
            spectator.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            SpectatorManager.OnSelfStartSpectating -= OnStartSpectating;
            SpectatorManager.OnSelfStopSpectating -= OnStopSpectating;
        }
    }
}
