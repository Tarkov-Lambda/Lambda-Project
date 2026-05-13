using System;
using TMPro;
using UnityEngine;
using Lambda.Shared;
using Lambda.Shared.Models;

namespace Lambda.UI
{
    public class TopBar : MonoBehaviour
    {
        [SerializeField] private TopBarTeamScore teamScoreLeft;
        [SerializeField] private TopBarTeamScore teamScoreRight;

        [SerializeField] private TMP_Text textTimer;
        [SerializeField] private float textTimerMonospacing = 16;

        [SerializeField] private FactionColors factionColors;

        [Space(10)]
        [SerializeField] private TeamStatus teamStatusLeft;
        [SerializeField] private TeamStatus teamStatusRight;

        void Start()
        {
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
            if (textTimer == null)
                return;
            textTimer.text = $"<mspace={textTimerMonospacing}>{FormatTime(seconds)}</mspace>";
        }

        public void ToggleTimer(bool show)
        {
            if (textTimer == null)
                return;
            textTimer.alpha = show ? 1f : 0f;
        }

        public void SetTeamStatuses(PlayerScoreInfo[] leftTeam, PlayerScoreInfo[] rightTeam)
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
