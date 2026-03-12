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
    }
}
