using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep
{
    /// <summary>
    /// Renders a clean in-game overlay for all data collected by DynamicClassTracer.
    /// Toggle with F3. Attach to any persistent GameObject (e.g. _tickerObject).
    ///
    /// Segmentation:
    ///   High Frequency  — methods called more than FREQ_THRESHOLD times/sec
    ///   Manual / Event  — everything else
    ///
    /// Each method name flashes red when called and fades back to white over 2 seconds.
    /// </summary>
    public class TracerOverlay : MonoBehaviour
    {
        // ── Config ───────────────────────────────────────────────────────────
        private const KeyCode ToggleKey      = KeyCode.F3;
        private const float FreqThreshold    = 5f;    // calls/sec threshold for "frequent"
        private const float FlashDuration    = 2f;    // seconds for red → white fade
        private const float ColdPruneAfter   = 3f;    // prune manual entries not called for this long
        private const float PanelWidth   = 560f;
        private const float RowHeight    = 24f;
        private const float HeaderHeight = 30f;
        private const float TitleHeight  = 32f;
        private const float SectionGap   = 6f;
        private const float PanelPadY    = 16f;

        // ── State ─────────────────────────────────────────────────────────
        private bool    _visible;
        private Vector2 _scrollHot  = Vector2.zero;
        private Vector2 _scrollCold = Vector2.zero;

        // Snapshots refreshed 4× per second — hold references into TracedData so
        // LastCallTime remains live for the flash effect.
        private List<TracedMethodInfo> _hotSnapshot  = new();
        private List<TracedMethodInfo> _coldSnapshot = new();
        private float _nextSnapshotTime;

        // ── Cached GUIStyles ─────────────────────────────────────────────
        private GUIStyle _styleTitle;
        private GUIStyle _styleSection;
        private GUIStyle _styleRow;
        private GUIStyle _styleCps;
        private GUIStyle _styleMuted;
        private bool _stylesInitialised;

        // Flash palette
        private static readonly Color ColorNormal = new Color(0.92f, 0.92f, 0.92f);
        private static readonly Color ColorFlash  = new Color(1.00f, 0.22f, 0.22f);

        // ── Unity ─────────────────────────────────────────────────────────

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                _visible = !_visible;

            if (!_visible) return;

            if (Time.realtimeSinceStartup >= _nextSnapshotTime)
                RefreshSnapshot();
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();
            DrawPanel();
        }

        // ── Snapshot ──────────────────────────────────────────────────────

        private void RefreshSnapshot()
        {
            _nextSnapshotTime = Time.realtimeSinceStartup + 0.25f;

            var all = DynamicClassTracer.TracedData.Values.ToList();

            // Both sections sorted alphabetically by method name
            _hotSnapshot = all
                .Where(m => m.CallsPerSecond >= FreqThreshold)
                .OrderBy(m => m.MethodName)
                .ToList();

            float now = Time.realtimeSinceStartup;
            _coldSnapshot = all
                .Where(m => m.CallsPerSecond < FreqThreshold
                         && (now - m.LastCallTime) <= ColdPruneAfter)
                .OrderBy(m => m.MethodName)
                .ToList();
        }

        // ── Drawing ───────────────────────────────────────────────────────

        private void DrawPanel()
        {
            float screenW = Screen.width;
            float screenH = Screen.height;

            float panelX   = screenW - PanelWidth - 16f;
            float panelY   = PanelPadY;
            float totalH   = screenH - PanelPadY * 2f;   // full screen height

            Rect panelRect = new Rect(panelX, panelY, PanelWidth, totalH);

            // Shadow
            DrawBox(new Rect(panelRect.x + 4, panelRect.y + 4, panelRect.width, panelRect.height),
                    new Color(0f, 0f, 0f, 0.30f));

            // Background
            DrawBox(panelRect, new Color(0.07f, 0.07f, 0.09f, 0.97f));

            // ── Title bar ───────────────────────────────────────────────
            Rect titleRect = new Rect(panelRect.x, panelRect.y, panelRect.width, TitleHeight);
            DrawBox(titleRect, new Color(0.12f, 0.12f, 0.17f, 1f));

            string types = DynamicClassTracer.TracerLabels.Count == 0
                ? "no active tracers"
                : string.Join(", ", DynamicClassTracer.TracerLabels.Values);

            GUI.Label(Inset(titleRect, 10, 0), $"🔍  TRACER  —  {types}", _styleTitle);
            GUI.Label(new Rect(titleRect.xMax - 90, titleRect.y, 82, TitleHeight), "F3 hide", _styleMuted);

            // ── Body: split remaining height evenly between the two sections ─
            float bodyTop    = panelRect.y + TitleHeight + 4f;
            float bodyBottom = panelRect.yMax - 4f;
            float bodyH      = bodyBottom - bodyTop;

            // Reserve space for the two section headers + gap
            float bodyAvail  = bodyH - HeaderHeight * 2f - SectionGap;
            float hotBodyH   = bodyAvail * 0.40f;   // hot usually has fewer rows
            float coldBodyH  = bodyAvail * 0.60f;

            float cursor = bodyTop;

            // ── Hot section ─────────────────────────────────────────────
            cursor = DrawSection(
                x: panelRect.x,
                y: cursor,
                width: panelRect.width,
                bodyH: hotBodyH,
                label: $"HIGH FREQUENCY  (> {FreqThreshold:0} / sec)",
                headerColor: new Color(0.50f, 0.22f, 0.04f, 0.95f),
                rows: _hotSnapshot,
                ref _scrollHot);

            cursor += SectionGap;

            // ── Cold section ─────────────────────────────────────────────
            DrawSection(
                x: panelRect.x,
                y: cursor,
                width: panelRect.width,
                bodyH: coldBodyH,
                label: "MANUAL / EVENT",
                headerColor: new Color(0.06f, 0.22f, 0.42f, 0.95f),
                rows: _coldSnapshot,
                ref _scrollCold);
        }

        private float DrawSection(
            float x, float y, float width, float bodyH,
            string label, Color headerColor,
            List<TracedMethodInfo> rows,
            ref Vector2 scroll)
        {
            // Section header
            Rect headerRect = new Rect(x, y, width, HeaderHeight);
            DrawBox(headerRect, headerColor);
            GUI.Label(Inset(headerRect, 10, 0), label, _styleSection);

            string countStr = rows.Count == 0 ? "none" : $"{rows.Count} methods";
            GUI.Label(new Rect(headerRect.xMax - 110, headerRect.y, 102, HeaderHeight),
                countStr, _styleMuted);

            float sectionTop = y + HeaderHeight;

            if (rows.Count == 0)
            {
                Rect emptyRect = new Rect(x, sectionTop, width, RowHeight);
                DrawBox(emptyRect, new Color(0.09f, 0.09f, 0.11f, 0.85f));
                GUI.Label(Inset(emptyRect, 14, 0), "—  nothing here yet", _styleMuted);
                return sectionTop + RowHeight;
            }

            float contentTotalH = rows.Count * RowHeight;
            bool needsScroll    = contentTotalH > bodyH;

            Rect viewRect    = new Rect(x, sectionTop, width, bodyH);
            Rect contentRect = new Rect(0, 0, width - (needsScroll ? 14f : 0f), contentTotalH);

            scroll = GUI.BeginScrollView(viewRect, scroll, contentRect, false, needsScroll);

            float now = Time.realtimeSinceStartup;

            for (int i = 0; i < rows.Count; i++)
            {
                var info = rows[i];
                bool alt = i % 2 == 1;
                Rect rowRect = new Rect(0, i * RowHeight, contentRect.width, RowHeight);

                DrawBox(rowRect, alt
                    ? new Color(0.09f, 0.09f, 0.12f, 0.85f)
                    : new Color(0.12f, 0.12f, 0.15f, 0.85f));

                // ── Flash: method name lerps red→white over FlashDuration seconds
                float timeSince = now - info.LastCallTime;
                float t         = Mathf.Clamp01(timeSince / FlashDuration);
                Color nameColor = Color.Lerp(ColorFlash, ColorNormal, t);

                // Method name — ~55 % width
                float nameW = rowRect.width * 0.55f;
                Rect  nameR = Inset(new Rect(rowRect.x, rowRect.y, nameW, RowHeight), 12, 0);
                DrawColoredLabel(nameR, info.MethodName, _styleRow, nameColor);

                // calls/sec — ~20 %
                float cpsX = rowRect.x + nameW;
                float cpsW = rowRect.width * 0.20f;
                string cpsStr = info.CallsPerSecond >= 1f
                    ? $"{info.CallsPerSecond:0.0}/s"
                    : info.CallsPerSecond > 0f ? "<1/s" : "0/s";
                GUI.Label(new Rect(cpsX, rowRect.y, cpsW, RowHeight), cpsStr, _styleCps);

                // total — remainder
                float totX = cpsX + cpsW;
                float totW = rowRect.width - nameW - cpsW;
                GUI.Label(new Rect(totX, rowRect.y, totW, RowHeight),
                    $"total: {info.TotalCalls:N0}", _styleMuted);
            }

            GUI.EndScrollView();
            return sectionTop + bodyH;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        /// Draw a label with an arbitrary text color without allocating a new GUIStyle.
        private static void DrawColoredLabel(Rect r, string text, GUIStyle style, Color color)
        {
            Color prev = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(r, text, style);
            GUI.contentColor = prev;
        }

        private static void DrawBox(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static Rect Inset(Rect r, float px, float py) =>
            new Rect(r.x + px, r.y + py, r.width - px * 2f, r.height - py * 2f);

        // ── Style init ────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (_stylesInitialised) return;
            _stylesInitialised = true;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.88f, 0.88f, 0.96f) }
            };

            _styleSection = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = Color.white }
            };

            _styleRow = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 18,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = ColorNormal }
            };

            _styleCps = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 17,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.65f, 0.92f, 0.65f) }
            };

            _styleMuted = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 16,
                alignment = TextAnchor.MiddleRight,
                normal    = { textColor = new Color(0.48f, 0.50f, 0.54f) }
            };
        }
    }
}
