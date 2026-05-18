using System;
using TMPro;
using UnityEngine;
using Lambda.Shared;
using Lambda.Shared.Models;

namespace Lambda.UI
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class TopBar : MonoBehaviour
    {
        [SerializeField] private TopBarTeamScore teamScoreLeft;
        [SerializeField] private TopBarTeamScore teamScoreRight;

        [field: SerializeField] public TMP_Text TextTimer { get; private set; }
        [SerializeField] private float textTimerMonospacing = 16;

        [SerializeField] private FactionColors factionColors;

        [Space(10)]
        [SerializeField] private TeamStatus teamStatusLeft;
        [SerializeField] private TeamStatus teamStatusRight;

        public RectTransform Rect { get; private set; }
        public CanvasGroup Canvas { get; private set; }

        void Awake()
        {
            Rect = GetComponent<RectTransform>();
            Canvas = GetComponent<CanvasGroup>();

            teamScoreLeft.Color = factionColors.Get(Faction.CT);
            teamScoreRight.Color = factionColors.Get(Faction.T);
        }

        public void SetScores(int left, int right)
        {
            teamScoreLeft?.Set(left);
            teamScoreRight?.Set(right);
        }

        public void SetTime(float seconds)
        {
            if (TextTimer == null)
                return;
            TextTimer.text = $"<mspace={textTimerMonospacing}>{FormatTime(seconds)}</mspace>";
        }

        public void SetTeamStatuses(PlayerContextInfo[] leftTeam, PlayerContextInfo[] rightTeam)
        {
            teamStatusLeft?.Set(leftTeam);
            teamStatusRight?.Set(rightTeam);
        }

        string FormatTime(float seconds)
        {
            if (seconds < 0) seconds = 0;
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return $"{t.Minutes:D2}:{t.Seconds:D2}";
        }
    }
}