using UnityEngine;
using UnityEngine.UI;

namespace arena.ui
{
    internal static class Extensions
    {
        public static void SetAlpha(this Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        public static void SetColorKeepGraphicAlpha(this Graphic graphic, Color color)
        {
            Color newColor = graphic.color;
            newColor.r = color.r;
            newColor.g = color.g;
            newColor.b = color.b;
            graphic.color = newColor;
        }
    }
}
