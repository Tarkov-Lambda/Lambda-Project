using Lambda.UI;
using EFT;
using System;

namespace Lambda.Core.Main.UI
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
            PlayerContext playerScore = H.GetPlayerContext(player);
            spectator.SetSpectatingPlayer(playerScore.Context);
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
