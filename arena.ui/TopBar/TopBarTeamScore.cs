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

        [SerializeField] private Color color;

        public Color Color
        {
            get => color;
            set
            {
                color = value;
                foreach (var graphic in coloredGraphics)
                {
                    if (graphic == null)
                        continue;


                    graphic.SetColorKeepGraphicAlpha(color);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Color = color;
        }
#endif

        public void Set(int score)
        {
            textScore.text = score.ToString();
        }
    }
}
