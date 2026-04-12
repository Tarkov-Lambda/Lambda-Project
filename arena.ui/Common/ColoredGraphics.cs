using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace arena.ui
{
    public class ColoredGraphics : MonoBehaviour
    {
        [SerializeField] private Graphic[] coloredGraphics;

        public void Set(Color color)
        {
            foreach (var graphic in coloredGraphics)
            {
                if (graphic == null)
                    continue;
                graphic.SetColorKeepGraphicAlpha(color);
            }
        }
    }
}
