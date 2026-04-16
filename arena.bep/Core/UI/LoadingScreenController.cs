using System;
using UnityEngine;

using EFT.UI;

using ifp.arena.bep.networking;
using arena.ui;

namespace ifp.arena.bep.Core.UI
{
    internal class LoadingScreenController : IDisposable
    {
        LoadingScreen screen;

        internal LoadingScreenController(CommonUI commonUI, AssetBundle uiBundle)
        {
            var prefab = uiBundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/LoadingScreen/LoadingScreen.prefab");
            screen = GameObject.Instantiate(prefab, commonUI.EftBattleUIScreen.transform).GetComponent<LoadingScreen>();

            screen.gameObject.SetActive(false);

            PlayerReadinessPacketHandler.AfterPacketApproved += Instance_AfterPacketApproved;
        }

        private void Instance_AfterPacketApproved(PlayerReadinessPacket packet)
        {
            int countReady = 0;
            string nameOfLastPlayerNotReady = "";
            foreach (var playerScore in H.Scoreboard.Values)
            {
                if (playerScore.ReadyState != PlayerReadinessState.Ready)
                {
                    nameOfLastPlayerNotReady = playerScore.player.Profile.Nickname;
                }

                if (playerScore.ReadyState == PlayerReadinessState.Ready)
                    countReady++;
            }

            bool loading = countReady != H.Scoreboard.Values.Count;

            screen.gameObject.SetActive(loading);
            if (!loading)
                return;

            bool waitingOnLastPlayer = H.Scoreboard.Count - countReady == 1;
            if (waitingOnLastPlayer)
            {
                screen.SetText($"Waiting on {nameOfLastPlayerNotReady}");
            }
            else
            {
                string textReadyPlayers = $"{countReady}/{H.Scoreboard.Count}";
                screen.SetText($"Waiting for players ({textReadyPlayers})");
            }
        }

        public void Dispose()
        {
            PlayerReadinessPacketHandler.AfterPacketApproved -= Instance_AfterPacketApproved;

            GameObject.Destroy(screen.gameObject);
        }
    }
}
