using UnityEngine;

namespace ifp.arena.bep
{
    public partial class TracerOverlay
    {
        internal static readonly Color ColorNormal   = new Color(0.92f, 0.92f, 0.92f);
        internal static readonly Color ColorFlash    = new Color(1.00f, 0.22f, 0.22f);
        internal static readonly Color ColorSelected = new Color(0.35f, 0.65f, 1.00f);
        internal static readonly Color ColorMuted    = new Color(0.48f, 0.50f, 0.54f);
        internal static readonly Color ColorPropVal  = new Color(0.60f, 0.92f, 0.60f);

        private GUIStyle _styleTitle;
        private GUIStyle _styleSection;
        private GUIStyle _styleRow;
        private GUIStyle _styleCps;
        private GUIStyle _styleMuted;

        private GUIStyle _styleDetailTitle;
        private GUIStyle _styleDetailColHdr;
        private GUIStyle _styleDetailAgo;
        private GUIStyle _styleDetailArg;
        private GUIStyle _styleDetailResult;

        private GUIStyle _stylePropName;
        private GUIStyle _stylePropValue;

        private bool _stylesInit;

        private void EnsureStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.88f, 0.88f, 0.96f) }
            };

            _styleSection = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = Color.white }
            };

            _styleRow = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 15,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = ColorNormal }
            };

            _styleCps = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.65f, 0.92f, 0.65f) }
            };

            _styleMuted = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                alignment = TextAnchor.MiddleRight,
                normal    = { textColor = ColorMuted }
            };

            _styleDetailTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.80f, 0.88f, 1.00f) }
            };

            _styleDetailColHdr = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.55f, 0.58f, 0.65f) }
            };

            _styleDetailAgo = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.50f, 0.53f, 0.60f) }
            };

            _styleDetailArg = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                alignment = TextAnchor.MiddleLeft,
                clipping  = TextClipping.Clip,
                normal    = { textColor = ColorNormal }
            };

            _styleDetailResult = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping  = TextClipping.Clip,
                normal    = { textColor = ColorPropVal }
            };

            _stylePropName = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                alignment = TextAnchor.MiddleLeft,
                clipping  = TextClipping.Clip,
                normal    = { textColor = new Color(0.80f, 0.84f, 0.92f) }
            };

            _stylePropValue = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping  = TextClipping.Clip,
                normal    = { textColor = ColorPropVal }
            };
        }

        internal static void DrawColoredLabel(Rect r, string text, GUIStyle style, Color color)
        {
            Color prev = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(r, text, style);
            GUI.contentColor = prev;
        }

        internal static void DrawBox(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        internal static Rect Inset(Rect r, float px, float py) =>
            new Rect(r.x + px, r.y + py, r.width - px * 2f, r.height - py * 2f);
    }
}
