using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace arena.ui
{
    public class TopBarTeamScore : MonoBehaviour
    {
        [SerializeField] private TMP_Text textScore;

        [SerializeField] private Graphic[] coloredGraphics;

        public Color color;

        private void OnValidate()
        {
            foreach (var graphic in coloredGraphics)
            {
                if (graphic == null)
                    continue;
                graphic.color = color;
            }
        }

        public void Set(int score)
        {
            textScore.text = score.ToString();
        }
    }
}
