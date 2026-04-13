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

            EventBus.OnPlayerKill += OnPlayerKill;
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

            Singleton<CommonUI>.Instance.EftBattleUIScreen.UpdatePanelsVisibility(false);
        }

        void OnSelfRespawn()
        {
            Singleton<CommonUI>.Instance.EftBattleUIScreen.UpdatePanelsVisibility(true);
        }

        public void Dispose()
        {
            EventBus.OnPlayerKill -= OnPlayerKill;
            EventBus.OnSelfRespawn -= OnSelfRespawn;
        }
    }
}
