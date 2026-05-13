using Lambda.UI;
using Comfort.Common;
using EFT;
using Lambda.Core.Patches.Tarkov;
using Lambda.Shared.Models;
using System;
using UnityEngine;
using System.Linq;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using Lambda.UI.scoreboard;
using DG.Tweening;

namespace Lambda.Core.Main.UI
{
    internal class ScoreboardController : IDisposable
    {
        readonly Scoreboard scoreboardUI;

        InventoryHotkeyListener inventoryHotkeyListener;

        bool forceVisibleByMatchState;

        internal ScoreboardController(Scoreboard scoreboardUI)
        {
            this.scoreboardUI = scoreboardUI;

            PlayerKilledPacketWarden.AfterPacketApplied += OnPlayerKill;
            PlayerReadinessPacketWarden.AfterPacketApplied += OnPlayerReadiness;

            EventBus.OnEnter += OnMatchStateEnter;
            EventBus.OnExit += OnMatchStateExit;

            Patch_Gameworld_OnGameStarted.OnGameStarted += AddInventoryHotkeyInterceptor;
            if (Singleton<GameWorld>.Instantiated)
                AddInventoryHotkeyInterceptor();

            scoreboardUI.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            PlayerKilledPacketWarden.AfterPacketApplied -= OnPlayerKill;
            PlayerReadinessPacketWarden.AfterPacketApplied -= OnPlayerReadiness;

            EventBus.OnEnter -= OnMatchStateEnter;
            EventBus.OnExit -= OnMatchStateExit;

            Patch_Gameworld_OnGameStarted.OnGameStarted -= AddInventoryHotkeyInterceptor;

            if (inventoryHotkeyListener != null)
                Component.Destroy(inventoryHotkeyListener);
        }

        private void OnPlayerKill(PlayerKilledPacket packet) => Refresh();
        private void OnPlayerReadiness(PlayerReadinessPacket packet) => Refresh();

        private void OnMatchStateEnter(MatchState state)
        {
            Refresh();

            if (state == MatchState.SideSwap || state == MatchState.Cleanup)
            {
                forceVisibleByMatchState = true;
                ToggleVisibility(true);
            }
        }

        private void OnMatchStateExit(MatchState state)
        {
            if (state == MatchState.SideSwap || state == MatchState.Cleanup)
            {
                forceVisibleByMatchState = false;
                ToggleVisibility(false);
            }
        }

        private void AddInventoryHotkeyInterceptor()
        {
            inventoryHotkeyListener = H.MainPlayer.gameObject.AddComponent<InventoryHotkeyListener>();

            inventoryHotkeyListener.OnHoldBegin += () =>
            {
                if (!forceVisibleByMatchState)
                    SetVisibleImmediate(true);
            };

            inventoryHotkeyListener.OnHoldEnd += () =>
            {
                if (!forceVisibleByMatchState)
                    SetVisibleImmediate(false);
            };
        }

        void SetVisibleImmediate(bool show)
        {
            if (forceVisibleByMatchState && !show)
                return;

            CanvasGroup canvasGroup = scoreboardUI.gameObject.GetOrAddComponent<CanvasGroup>();
            canvasGroup.DOKill();

            scoreboardUI.gameObject.SetActive(show);
            canvasGroup.alpha = show ? 1f : 0f;
        }

        void Refresh()
        {
            PlayerScoreInfo[] allPlayerStats = H.Scoreboard.Values
                .Select(p => p.Score)
                .ToArray();

            scoreboardUI.SetPlayers(
                allPlayerStats,
                H.Session.factionWins,
                H.MainPlayerScore.Faction);
        }

        void ToggleVisibility(bool show)
        {
            if (forceVisibleByMatchState && !show)
                return;

            float fadeAlphaTarget = show ? 1f : 0f;
            float fadeDuration = 0.3f;

            CanvasGroup canvasGroup = scoreboardUI.gameObject.GetOrAddComponent<CanvasGroup>();
            canvasGroup.DOKill();

            if (show)
            {
                scoreboardUI.gameObject.SetActive(true);
                canvasGroup.alpha = 0f;
            }

            canvasGroup.DOFade(fadeAlphaTarget, fadeDuration)
                .OnComplete(() =>
                {
                    if (!show)
                        scoreboardUI.gameObject.SetActive(false);
                });
        }
    }
}