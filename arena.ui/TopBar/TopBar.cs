using System;
using TMPro;
using UnityEngine;

namespace arena.ui
{
    public class TopBar : MonoBehaviour
    {
        [SerializeField] private TopBarTeamScore teamScoreLeft;
        [SerializeField] private TopBarTeamScore teamScoreRight;

        [SerializeField] private TMP_Text textTimer;

        public void SetScores(int left, int right)
        {
            teamScoreLeft.Set(left);
            teamScoreRight.Set(right);
        }

        public void SetTime(float seconds)
        {
            if (textTimer == null)
                return;
            textTimer.text = $"<mspace=21>{FormatTime(seconds)}</mspace>";
        }

        string FormatTime(float seconds)
        {
            if (seconds < 0) seconds = 0;
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return $"{t.Minutes:D2}:{t.Seconds:D2}";
        }
    }
}
