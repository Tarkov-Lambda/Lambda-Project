using Lambda.UI;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using Lambda.Shared.Models;
using System;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using Comfort.Common;

namespace Lambda.Core.Main.UI
{
    internal class TopBarController : IDisposable
    {
        readonly TopBar topBar;

        private bool isVisible = true;
        private bool isTimerVisible = true;

        private Tween visibilityTween;
        private Tween timerSizeTween;
        private Tween timerAlphaTween;

        private const float AnimDuration = 0.35f;

        internal TopBarController(TopBar topBar)
        {
            this.topBar = topBar;

            Singleton<PlayerKilledPacketWarden>.Instance.AfterPacketApplied += OnPlayerKill;
            Singleton<PlayerReadinessPacketWarden>.Instance.AfterPacketApplied += OnPlayerReadiness;
            EventBus.OnEnter += OnMatchStateEnter;
            UnityTicker.OnUpdate += OnUpdate;
        }

        public void Dispose()
        {
            Singleton<PlayerKilledPacketWarden>.Instance.AfterPacketApplied -= OnPlayerKill;
            Singleton<PlayerReadinessPacketWarden>.Instance.AfterPacketApplied -= OnPlayerReadiness;
            EventBus.OnEnter -= OnMatchStateEnter;
            UnityTicker.OnUpdate -= OnUpdate;

            visibilityTween?.Kill();
            timerSizeTween?.Kill();
            timerAlphaTween?.Kill();
        }

        private void OnPlayerKill(PlayerKilledPacket packet) => Refresh();
        private void OnPlayerReadiness(PlayerReadinessPacket packet) => Refresh();

        private void OnMatchStateEnter(MatchState state)
        {
            if (state is MatchState.None or MatchState.Cleanup or MatchState.SideSwap or MatchState.MatchEnd)
            {
                ToggleVisibility(false);
            }
            else
            {
                ToggleVisibility(true);
            }

            if (state is MatchState.Cleanup or MatchState.SideSwap or MatchState.RoundPlanted or MatchState.RoundEnd or MatchState.MatchEnd)
            {
                ToggleTimer(false);
            }
            else
            {
                ToggleTimer(true);
            }

            Refresh();
        }

        void Refresh()
        {
            int scoreCT = H.Session.factionWins[Faction.CT];
            int scoreT = H.Session.factionWins[Faction.T];

            topBar.SetScores(scoreCT, scoreT);

            PlayerContextInfo[] allPlayerStats = H.Scoreboard.Values.Select(p => p.Context).ToArray();

            PlayerContextInfo[] teamT = allPlayerStats.Where(p => p.Faction == Faction.T).ToArray();
            PlayerContextInfo[] teamCT = allPlayerStats.Where(p => p.Faction == Faction.CT).ToArray();

            topBar.SetTeamStatuses(teamCT, teamT);
        }

        private void ToggleVisibility(bool show)
        {
            if (isVisible == show) return;
            isVisible = show;

            visibilityTween?.Kill();
            topBar.Canvas.blocksRaycasts = show;

            float targetAlpha = show ? 1f : 0f;
            float delay = 0f;

            if (!show && isTimerVisible)
            {
                ToggleTimer(false);
                delay = AnimDuration;
            }

            visibilityTween = topBar.Canvas.DOFade(targetAlpha, AnimDuration)
                .SetDelay(delay)
                .SetEase(Ease.InOutSine);
        }

        private void ToggleTimer(bool show)
        {
            if (isTimerVisible == show) return;
            isTimerVisible = show;

            timerSizeTween?.Kill();
            timerAlphaTween?.Kill();

            float targetWidth = show ? 210f : 160f;
            float targetAlpha = show ? 1f : 0f;

            timerSizeTween = topBar.Rect.DOSizeDelta(new Vector2(targetWidth, topBar.Rect.sizeDelta.y), AnimDuration).SetEase(Ease.OutCubic);

            if (show)
            {
                timerAlphaTween = topBar.TextTimer.DOFade(targetAlpha, AnimDuration).SetEase(Ease.InOutSine);
            }
            else
            {
                timerAlphaTween = topBar.TextTimer.DOFade(targetAlpha, AnimDuration * 0.3f).SetEase(Ease.OutExpo);
            }
        }

        private void OnUpdate()
        {
            if (isTimerVisible)
            {
                topBar.SetTime(H.Arena.StateTimer);
            }
        }
    }
}