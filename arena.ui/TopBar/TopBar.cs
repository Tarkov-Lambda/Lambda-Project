using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace arena.ui
{
    public class TopBar : MonoBehaviour
    {
        [SerializeField] private TopBarTeamScore teamScoreLeft;
        [SerializeField] private TopBarTeamScore teamScoreRight;

        public void SetScores(int left, int right)
        {
            teamScoreLeft.Set(left);
            teamScoreRight.Set(right);
        }
    }
}
