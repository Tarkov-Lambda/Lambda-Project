using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace arena.ui
{
    public class TopBarTeamScore : MonoBehaviour
    {
        [SerializeField] private TMP_Text textScore;

        public void Set(int score)
        {
            textScore.text = score.ToString();
        }
    }
}
