using Lambda.UI;
using Comfort.Common;
using EFT.UI;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using Lambda.Shared.Models;
using System;

namespace Lambda.Core.Main.UI
{
    internal class SelfDeathController : IDisposable
    {
        readonly DeathInfo deathInfo;

        internal SelfDeathController(DeathInfo deathInfo)
        {
            this.deathInfo = deathInfo;

            PlayerKilledPacketWarden.AfterPacketApplied += OnPlayerKill;
            EventBus.OnSelfRespawn += OnSelfRespawn;
        }

        void OnPlayerKill(PlayerKilledPacket killPacket)
        {
            if (killPacket.Player == H.MainPlayer)
            {
                PlayerScore killerScore = H.GetPlayerScore(killPacket.killer);
                OnSelfDeath(killerScore.Score);
            }
        }

        void OnSelfDeath(PlayerScoreInfo killer)
        {
            deathInfo.Pop(killer);

            TryToggleTarkovBattleUI(false);
        }

        void OnSelfRespawn()
        {
            TryToggleTarkovBattleUI(true);
        }

        void TryToggleTarkovBattleUI(bool toggle)
        {
            try
            {
                Singleton<CommonUI>.Instance.EftBattleUIScreen.UpdatePanelsVisibility(toggle);
            }
            catch { }
        }

        public void Dispose()
        {
            PlayerKilledPacketWarden.AfterPacketApplied -= OnPlayerKill;
            EventBus.OnSelfRespawn -= OnSelfRespawn;
        }
    }
}
