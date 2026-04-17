using arena.ui;
using Comfort.Common;
using EFT.UI;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using ifp.arena.shared.Models;
using System;

namespace ifp.arena.bep.Core.UI
{
    internal class SelfDeathController : IDisposable
    {
        readonly DeathInfo deathInfo;

        internal SelfDeathController(DeathInfo deathInfo)
        {
            this.deathInfo = deathInfo;

            PlayerKilledPacketHandler.AfterPacketApplied += OnPlayerKill;
            EventBus.OnSelfRespawn += OnSelfRespawn;
        }

        void OnPlayerKill(PlayerKilledPacket killPacket)
        {
            if (killPacket.victim == H.MainPlayer)
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
            PlayerKilledPacketHandler.AfterPacketApplied -= OnPlayerKill;
            EventBus.OnSelfRespawn -= OnSelfRespawn;
        }
    }
}
