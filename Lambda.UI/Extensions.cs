using UnityEngine;
using UnityEngine.UI;

namespace Lambda.UI
{
    internal static class Extensions
    {
        public static void SetAlpha(this Graphic graphic, float alpha)
        {
            if (graphic == null) return;

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        public static void SetColorKeepGraphicAlpha(this Graphic graphic, Color color)
        {
            if (graphic == null) return;

            Color newColor = graphic.color;
            newColor.r = color.r;
            newColor.g = color.g;
            newColor.b = color.b;
            graphic.color = newColor;
        }

        public static void SetColoredGraphicsColor(this Component component, Color color)
        {
            if (component.TryGetComponent<ColoredGraphics>(out var coloredGraphics))
            {
                coloredGraphics.Set(color);
            }
        }
    }
}
