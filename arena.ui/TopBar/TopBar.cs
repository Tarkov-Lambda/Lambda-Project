using System;
using TMPro;
using UnityEngine;
using ifp.arena.shared;

namespace arena.ui
{
    public class TopBar : MonoBehaviour
    {
        [SerializeField] private TopBarTeamScore teamScoreLeft;
        [SerializeField] private TopBarTeamScore teamScoreRight;

        [SerializeField] private TMP_Text textTimer;
        [SerializeField] private float textTimerMonospacing = 16;

        [SerializeField] private FactionColors factionColors;

        void OnEnable()
        {
            teamScoreLeft.Color = factionColors.Get(Faction.CT);
            teamScoreRight.Color = factionColors.Get(Faction.T);
        }

        public void SetScores(int left, int right)
        {
            teamScoreLeft.Set(left);
            teamScoreRight.Set(right);
        }

        public void SetTime(float seconds)
        {
            if (textTimer == null)
                return;
            textTimer.text = $"<mspace={textTimerMonospacing}>{FormatTime(seconds)}</mspace>";
        }

        string FormatTime(float seconds)
        {
            if (seconds < 0) seconds = 0;
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return $"{t.Minutes:D2}:{t.Seconds:D2}";
        }
    }
}
