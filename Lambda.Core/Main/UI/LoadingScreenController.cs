using System;
using UnityEngine;

using EFT.UI;
using DG.Tweening;

using Lambda.UI;
using Lambda.Core.Networking;
using Lambda.Core.Main.Gamemode;
using Comfort.Common;

namespace Lambda.Core.Main.UI
{
    internal class LoadingScreenController : IDisposable
    {
        LoadingScreen screen;

        bool selfCurrentlyLoadingMap;

        internal LoadingScreenController(CommonUI commonUI, AssetBundle uiBundle)
        {
            var prefab = uiBundle.LoadAsset<GameObject>("Packages/com.lambda.ui/LoadingScreen/LoadingScreen.prefab");
            screen = GameObject.Instantiate(prefab, commonUI.EftBattleUIScreen.transform).GetComponent<LoadingScreen>();
            screen.gameObject.SetActive(false);

            Singleton<PlayerReadinessPacketWarden>.Instance.AfterPacketApplied += ReceivePlayerReadinessPacket;
            EventBus.OnEnter += OnMatchStateEnter;
            EventBus.OnExit += OnMatchStateExit;
            MapLoadEvent.OnBeginLoad += OnBeginLoad;
            MapLoadEvent.OnSuccessfulLoad += OnSuccessfulMapLoad;
        }

        public void Dispose()
        {
            Singleton<PlayerReadinessPacketWarden>.Instance.AfterPacketApplied -= ReceivePlayerReadinessPacket;
            EventBus.OnEnter -= OnMatchStateEnter;
            MapLoadEvent.OnBeginLoad -= OnBeginLoad;
            MapLoadEvent.OnSuccessfulLoad -= OnSuccessfulMapLoad;

            GameObject.Destroy(screen.gameObject);
        }

        private void OnBeginLoad()
        {
            selfCurrentlyLoadingMap = true;

            screen.SetText("Loading map");
            ToggleVisibility(true);
        }

        private void OnSuccessfulMapLoad()
        {
            selfCurrentlyLoadingMap = false;

            if (IsWaitingOnOtherPlayers(out string text))
                screen.SetText(text);
        }

        private void ReceivePlayerReadinessPacket(PlayerReadinessPacket packet)
        {
            if (!selfCurrentlyLoadingMap)
            {
                if (IsWaitingOnOtherPlayers(out string text))
                    screen.SetText(text);
            }
        }

        private void OnMatchStateEnter(MatchState state)
        {
            if (state == MatchState.WarmupEnd)
                ToggleVisibility(false);

            if (state == MatchState.SideSwap)
                ToggleVisibility(true);

            if (state == MatchState.Cleanup)
                ToggleVisibility(true);
        }

        private void OnMatchStateExit(MatchState state)
        {
            if (state == MatchState.Cleanup)
                ToggleVisibility(false);
        }


        bool IsWaitingOnOtherPlayers(out string text)
        {
            text = string.Empty;

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

            if (countReady == H.Scoreboard.Values.Count)
                return false;

            bool waitingOnLastPlayer = (H.Scoreboard.Count - countReady) == 1;
            if (waitingOnLastPlayer)
            {
                text = $"Waiting on {nameOfLastPlayerNotReady}";
            }
            else
            {
                string textReadyPlayers = $"{countReady}/{H.Scoreboard.Count}";
                text = $"Waiting for players ({textReadyPlayers})";
            }
            return true;
        }

        void ToggleVisibility(bool show)
        {
            float fadeAlphaTarget = show ? 1f : 0f;
            float fadeDuration = 0.3f;

            CanvasGroup canvasGroup = screen.gameObject.GetOrAddComponent<CanvasGroup>();
            canvasGroup.DOKill();

            if (show)
            {
                screen.gameObject.SetActive(true);
                canvasGroup.alpha = 0f;
            }

            canvasGroup.DOFade(fadeAlphaTarget, fadeDuration)
                .OnComplete(() =>
                {
                    if (!show)
                        screen.gameObject.SetActive(false);
                });
        }
    }
}
