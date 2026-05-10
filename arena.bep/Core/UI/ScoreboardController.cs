using arena.ui;
using Comfort.Common;
using EFT;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared.Models;
using System;
using UnityEngine;
using System.Linq;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using arena.ui.scoreboard;
using DG.Tweening;

namespace ifp.arena.bep.Core.UI
{
    internal class ScoreboardController : IDisposable
    {
        readonly Scoreboard scoreboardUI;

        InventoryHotkeyListener inventoryHotkeyListener;

        bool forceVisibleByMatchState;

        internal ScoreboardController(Scoreboard scoreboardUI)
        {
            this.scoreboardUI = scoreboardUI;

            PlayerKilledPacketHandler.AfterPacketApplied += OnPlayerKill;
            PlayerReadinessPacketHandler.AfterPacketApplied += OnPlayerReadiness;

            EventBus.OnEnter += OnMatchStateEnter;
            EventBus.OnExit += OnMatchStateExit;

            Patch_Gameworld_OnGameStarted.OnGameStarted += AddInventoryHotkeyInterceptor;
            if (Singleton<GameWorld>.Instantiated)
                AddInventoryHotkeyInterceptor();

            scoreboardUI.gameObject.SetActive(false);
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

        public void Dispose()
        {
            PlayerKilledPacketHandler.AfterPacketApplied -= OnPlayerKill;
            PlayerReadinessPacketHandler.AfterPacketApplied -= OnPlayerReadiness;

            EventBus.OnEnter -= OnMatchStateEnter;
            EventBus.OnExit -= OnMatchStateExit;

            Patch_Gameworld_OnGameStarted.OnGameStarted -= AddInventoryHotkeyInterceptor;

            if (inventoryHotkeyListener != null)
                Component.Destroy(inventoryHotkeyListener);
        }
    }
}