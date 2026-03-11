using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep
{

    public class TracerOverlay : MonoBehaviour
    {
        // ── Config ───────────────────────────────────────────────────────────
        private const KeyCode ToggleKey = KeyCode.F3;
        private const float FreqThreshold = 5f;
        private const float FlashDuration = 2f;
        private const float ColdPruneAfter = 3f;

        private const float PanelWidth = 560f;
        private const float DetailWidth = 1200f;
        private const float RowHeight = 24f;
        private const float RecordRowH = 22f;
        private const float HeaderHeight = 30f;
        private const float TitleHeight = 32f;
        private const float SectionGap = 6f;
        private const float PanelPadY = 16f;

        private const int HotDisplayCount = 5;    // last N throttled samples for hot methods
        private const int ColdDisplayCount = 20;   // last N calls for cold methods

        // ── State ─────────────────────────────────────────────────────────
        private bool _visible;
        private Vector2 _scrollHot = Vector2.zero;
        private Vector2 _scrollCold = Vector2.zero;
        private Vector2 _scrollDetail = Vector2.zero;
        private string _selectedKey = null;              // "TypeName.MethodName"
        private readonly HashSet<float> _expandedRecords = new HashSet<float>(); // record timestamps
        private TracedCallRecord[] _frozenSnapshot = null;                 // non-null = display paused

        private List<TracedMethodInfo> _hotSnapshot = new();
        private List<TracedMethodInfo> _coldSnapshot = new();
        private float _nextSnapshotTime;

        // ── GUIStyles ─────────────────────────────────────────────────────
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
        private bool _stylesInit;

        private static readonly Color ColorNormal = new Color(0.92f, 0.92f, 0.92f);
        private static readonly Color ColorFlash = new Color(1.00f, 0.22f, 0.22f);
        private static readonly Color ColorSelected = new Color(0.35f, 0.65f, 1.00f);

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
            DrawMainPanel();
            if (_selectedKey != null) DrawDetailPanel();
        }

        // ── Snapshot ──────────────────────────────────────────────────────

        private void RefreshSnapshot()
        {
            _nextSnapshotTime = Time.realtimeSinceStartup + 0.25f;

            var all = DynamicClassTracer.TracedData.Values.ToList();

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

        // ── Main panel ────────────────────────────────────────────────────

        private void DrawMainPanel()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            float px = sw - PanelWidth - 16f;
            float py = PanelPadY;
            float ph = sh - PanelPadY * 2f;

            Rect panel = new Rect(px, py, PanelWidth, ph);

            // Shadow
            DrawBox(new Rect(px + 4, py + 4, PanelWidth, ph), new Color(0, 0, 0, 0.30f));
            DrawBox(panel, new Color(0.07f, 0.07f, 0.09f, 0.97f));

            // Title bar
            Rect titleR = new Rect(px, py, PanelWidth, TitleHeight);
            DrawBox(titleR, new Color(0.12f, 0.12f, 0.17f, 1f));

            string types = DynamicClassTracer.TracerLabels.Count == 0
                ? "no active tracers"
                : string.Join(", ", DynamicClassTracer.TracerLabels.Values);
            GUI.Label(Inset(titleR, 10, 0), $"🔍  TRACER  —  {types}", _styleTitle);
            GUI.Label(new Rect(titleR.xMax - 90, titleR.y, 82, TitleHeight), "F3 hide", _styleMuted);

            float bodyTop = py + TitleHeight + 4f;
            float bodyAvail = (ph - 4f) - bodyTop + py - HeaderHeight * 2f - SectionGap;
            float hotBodyH = bodyAvail * 0.40f;
            float cldBodyH = bodyAvail * 0.60f;

            float cur = bodyTop;

            cur = DrawSection(px, cur, PanelWidth, hotBodyH,
                $"HIGH FREQUENCY  (> {FreqThreshold:0} / sec)",
                new Color(0.50f, 0.22f, 0.04f, 0.95f),
                _hotSnapshot, ref _scrollHot);

            cur += SectionGap;

            DrawSection(px, cur, PanelWidth, cldBodyH,
                "MANUAL / EVENT",
                new Color(0.06f, 0.22f, 0.42f, 0.95f),
                _coldSnapshot, ref _scrollCold);
        }

        private float DrawSection(
            float x, float y, float w, float bodyH,
            string label, Color headerColor,
            List<TracedMethodInfo> rows, ref Vector2 scroll)
        {
            Rect hdr = new Rect(x, y, w, HeaderHeight);
            DrawBox(hdr, headerColor);
            GUI.Label(Inset(hdr, 10, 0), label, _styleSection);
            GUI.Label(new Rect(hdr.xMax - 110, hdr.y, 102, HeaderHeight),
                rows.Count == 0 ? "none" : $"{rows.Count} methods", _styleMuted);

            float top = y + HeaderHeight;

            if (rows.Count == 0)
            {
                Rect empty = new Rect(x, top, w, RowHeight);
                DrawBox(empty, new Color(0.09f, 0.09f, 0.11f, 0.85f));
                GUI.Label(Inset(empty, 14, 0), "—  nothing here yet", _styleMuted);
                return top + RowHeight;
            }

            bool needsScroll = rows.Count * RowHeight > bodyH;
            Rect viewR = new Rect(x, top, w, bodyH);
            Rect contentR = new Rect(0, 0, w - (needsScroll ? 14f : 0f), rows.Count * RowHeight);

            scroll = GUI.BeginScrollView(viewR, scroll, contentR, false, needsScroll);

            float now = Time.realtimeSinceStartup;

            for (int i = 0; i < rows.Count; i++)
            {
                var info = rows[i];
                bool alt = i % 2 == 1;
                bool selected = (_selectedKey == MKey(info));
                Rect row = new Rect(0, i * RowHeight, contentR.width, RowHeight);

                // Background
                Color bg = selected
                    ? new Color(0.14f, 0.22f, 0.40f, 0.98f)
                    : alt
                        ? new Color(0.09f, 0.09f, 0.12f, 0.85f)
                        : new Color(0.12f, 0.12f, 0.15f, 0.85f);
                DrawBox(row, bg);

                // Invisible click-target
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    string newKey = selected ? null : MKey(info);
                    if (newKey != _selectedKey)
                    {
                        _expandedRecords.Clear();
                        _frozenSnapshot = null;
                    }
                    _selectedKey = newKey;
                    _scrollDetail = Vector2.zero;
                }

                // Selection arrow
                if (selected)
                {
                    Color prev = GUI.contentColor;
                    GUI.contentColor = ColorSelected;
                    GUI.Label(new Rect(row.x + 2, row.y, 12, RowHeight), "▶", _styleMuted);
                    GUI.contentColor = prev;
                }

                // Method name with flash
                float t = Mathf.Clamp01((now - info.LastCallTime) / FlashDuration);
                Color nameColor = selected ? ColorSelected : Color.Lerp(ColorFlash, ColorNormal, t);

                float nameW = row.width * 0.55f;
                DrawColoredLabel(
                    Inset(new Rect(row.x + 14, row.y, nameW - 14, RowHeight), 2, 0),
                    info.MethodName, _styleRow, nameColor);

                // calls/sec
                float cpsX = row.x + nameW;
                float cpsW = row.width * 0.20f;
                string cpsStr = info.CallsPerSecond >= 1f ? $"{info.CallsPerSecond:0.0}/s"
                              : info.CallsPerSecond > 0f ? "<1/s" : "0/s";
                GUI.Label(new Rect(cpsX, row.y, cpsW, RowHeight), cpsStr, _styleCps);

                // total
                float totX = cpsX + cpsW;
                GUI.Label(new Rect(totX, row.y, row.width - nameW - cpsW, RowHeight),
                    $"total: {info.TotalCalls:N0}", _styleMuted);
            }

            GUI.EndScrollView();
            return top + bodyH;
        }

        // ── Detail panel ──────────────────────────────────────────────────

        private void DrawDetailPanel()
        {
            if (!DynamicClassTracer.TracedData.TryGetValue(_selectedKey, out var info))
            {
                _selectedKey = null;
                return;
            }

            float sw = Screen.width;
            float sh = Screen.height;

            float px = sw - PanelWidth - 16f;
            float dx = Mathf.Max(4f, px - DetailWidth - 8f);
            float dy = PanelPadY;
            float dh = sh - PanelPadY * 2f;

            Rect detailPanel = new Rect(dx, dy, DetailWidth, dh);

            // Shadow + bg
            DrawBox(new Rect(dx + 4, dy + 4, DetailWidth, dh), new Color(0, 0, 0, 0.30f));
            DrawBox(detailPanel, new Color(0.06f, 0.06f, 0.09f, 0.97f));

            // ── Title bar ───────────────────────────────────────────────
            bool isHot = info.CallsPerSecond >= FreqThreshold;
            int showCount = isHot ? HotDisplayCount : ColdDisplayCount;
            string subtitle = isHot
                ? $"last {HotDisplayCount} samples  (≈0.1s apart)"
                : $"last {ColdDisplayCount} calls";

            Rect titleR = new Rect(dx, dy, DetailWidth, TitleHeight);
            DrawBox(titleR, new Color(0.10f, 0.16f, 0.28f, 1f));
            GUI.Label(Inset(titleR, 10, 0), $"📄  {info.MethodName}", _styleDetailTitle);
            GUI.Label(new Rect(titleR.xMax - 210, titleR.y, 202, TitleHeight), subtitle, _styleMuted);

            // ── Hint bar ────────────────────────────────────────────────
            float hintY = dy + TitleHeight;
            Rect hintR = new Rect(dx, hintY, DetailWidth, RecordRowH);
            bool frozen = _frozenSnapshot != null;
            DrawBox(hintR, frozen
                ? new Color(0.22f, 0.16f, 0.04f, 1f)    // warm amber when frozen
                : new Color(0.09f, 0.09f, 0.13f, 1f));
            GUI.Label(Inset(hintR, 8, 0),
                frozen
                    ? "⏸  FROZEN  —  collapse all records to resume live"
                    : "click a record to expand  ·  click again to collapse",
                _styleDetailColHdr);

            // ── Records ──────────────────────────────────────────────────
            float bodyTop = hintY + RecordRowH;
            float bodyH = detailPanel.yMax - bodyTop;

            // Use frozen snapshot while any record is expanded; live otherwise
            var records = _frozenSnapshot ?? info.History.GetSnapshot(showCount);

            // Dynamic content height: expanded records take more space
            float totalH = 0f;
            for (int i = 0; i < records.Length; i++)
            {
                bool exp = _expandedRecords.Contains(records[i].Timestamp);
                int argCount = records[i].Args?.Length ?? 0;
                // header + (argCount rows or 1 "no args" row) + 1 result row
                totalH += exp ? RecordRowH * (2 + Mathf.Max(argCount, 1)) : RecordRowH;
            }
            totalH = Mathf.Max(totalH, bodyH);

            bool needsScroll = totalH > bodyH;
            Rect viewR = new Rect(dx, bodyTop, DetailWidth, bodyH);
            Rect contentR = new Rect(0, 0, DetailWidth - (needsScroll ? 14f : 0f), totalH);

            float now = Time.realtimeSinceStartup;

            _scrollDetail = GUI.BeginScrollView(viewR, _scrollDetail, contentR, false, needsScroll);

            if (records.Length == 0)
            {
                DrawBox(new Rect(0, 0, contentR.width, RecordRowH), new Color(0.09f, 0.09f, 0.11f, 0.85f));
                GUI.Label(Inset(new Rect(0, 0, contentR.width, RecordRowH), 14, 0),
                    "no history yet — waiting for calls…", _styleMuted);
            }
            else
            {
                float curY = 0f;
                bool altBase = false;

                for (int i = 0; i < records.Length; i++)
                {
                    var rec = records[i];
                    bool expanded = _expandedRecords.Contains(rec.Timestamp);
                    int argCount = rec.Args?.Length ?? 0;

                    float age = now - rec.Timestamp;
                    float ft = Mathf.Clamp01(age / FlashDuration);
                    Color valueCol = Color.Lerp(ColorFlash, ColorNormal, ft);

                    string agoStr = age < 1f ? $"{age * 1000f:0}ms"
                                  : age < 60f ? $"{age:0.0}s"
                                  : "old";

                    // ── Record header row (always visible, clickable) ────
                    Color headerBg = altBase
                        ? new Color(0.09f, 0.09f, 0.13f, 0.92f)
                        : new Color(0.12f, 0.12f, 0.17f, 0.92f);
                    Rect headerRow = new Rect(0, curY, contentR.width, RecordRowH);
                    DrawBox(headerRow, headerBg);

                    // Click target for expand/collapse
                    if (GUI.Button(headerRow, GUIContent.none, GUIStyle.none))
                    {
                        if (expanded)
                        {
                            _expandedRecords.Remove(rec.Timestamp);
                            // Unfreeze once nothing is expanded anymore
                            if (_expandedRecords.Count == 0)
                                _frozenSnapshot = null;
                        }
                        else
                        {
                            // Freeze the list the moment the first record is opened
                            if (_frozenSnapshot == null)
                                _frozenSnapshot = info.History.GetSnapshot(showCount);
                            _expandedRecords.Add(rec.Timestamp);
                        }
                    }

                    // Expand arrow
                    string arrow = expanded ? "▼" : "▶";
                    DrawColoredLabel(new Rect(headerRow.x + 6, headerRow.y, 16, RecordRowH),
                        arrow, _styleDetailAgo,
                        expanded ? ColorSelected : new Color(0.42f, 0.44f, 0.50f));

                    // Ago
                    GUI.Label(new Rect(headerRow.x + 24, headerRow.y, 52, RecordRowH),
                        agoStr, _styleDetailAgo);

                    if (!expanded)
                    {
                        // Collapsed: single-line summary
                        float sumX = headerRow.x + 80f;
                        float resW = 100f;
                        float sumW = contentR.width - 80f - resW - 4f;

                        string argSummary = argCount == 0 ? "(no args)"
                            : string.Join(",  ", rec.Args);
                        DrawColoredLabel(new Rect(sumX, headerRow.y, sumW, RecordRowH),
                            argSummary, _styleDetailArg, valueCol);
                        DrawColoredLabel(new Rect(contentR.width - resW, headerRow.y, resW, RecordRowH),
                            rec.Result ?? "(void)", _styleDetailResult, valueCol);
                    }

                    curY += RecordRowH;

                    // ── Expanded sub-rows ────────────────────────────────
                    if (expanded)
                    {
                        const float indent = 28f;
                        const float typeW = 34f;    // "arg" / "↩" label
                        const float nameW = 130f;   // parameter name
                        const float eqW = 16f;

                        Color subBg = altBase
                            ? new Color(0.07f, 0.07f, 0.10f, 0.92f)
                            : new Color(0.09f, 0.09f, 0.13f, 0.92f);

                        // Argument rows
                        if (argCount == 0)
                        {
                            Rect noArgR = new Rect(0, curY, contentR.width, RecordRowH);
                            DrawBox(noArgR, subBg);
                            DrawColoredLabel(new Rect(indent, curY, contentR.width - indent, RecordRowH),
                                "(no arguments)", _styleDetailAgo, new Color(0.42f, 0.44f, 0.50f));
                            curY += RecordRowH;
                        }
                        else
                        {
                            foreach (string argStr in rec.Args)
                            {
                                Rect argR = new Rect(0, curY, contentR.width, RecordRowH);
                                DrawBox(argR, subBg);

                                // "arg" tag
                                DrawColoredLabel(new Rect(indent, curY, typeW, RecordRowH),
                                    "arg", _styleDetailColHdr, new Color(0.40f, 0.43f, 0.52f));

                                // Split "paramName=value" at first '='
                                int eqIdx = argStr.IndexOf('=');
                                if (eqIdx > 0)
                                {
                                    string pName = argStr.Substring(0, eqIdx);
                                    string pVal = argStr.Substring(eqIdx + 1);

                                    GUI.Label(new Rect(indent + typeW, curY, nameW, RecordRowH),
                                        pName, _styleDetailAgo);
                                    DrawColoredLabel(new Rect(indent + typeW + nameW, curY, eqW, RecordRowH),
                                        "=", _styleDetailColHdr, new Color(0.38f, 0.40f, 0.46f));
                                    DrawColoredLabel(
                                        new Rect(indent + typeW + nameW + eqW, curY,
                                            contentR.width - indent - typeW - nameW - eqW, RecordRowH),
                                        pVal, _styleDetailArg, valueCol);
                                }
                                else
                                {
                                    DrawColoredLabel(
                                        new Rect(indent + typeW, curY, contentR.width - indent - typeW, RecordRowH),
                                        argStr, _styleDetailArg, valueCol);
                                }

                                curY += RecordRowH;
                            }
                        }

                        // Result row
                        Rect resR = new Rect(0, curY, contentR.width, RecordRowH);
                        DrawBox(resR, subBg);
                        DrawColoredLabel(new Rect(indent, curY, typeW, RecordRowH),
                            "↩", _styleDetailColHdr, new Color(0.40f, 0.65f, 0.40f));
                        DrawColoredLabel(
                            new Rect(indent + typeW, curY, contentR.width - indent - typeW, RecordRowH),
                            rec.Result ?? "(void)", _styleDetailResult, valueCol);
                        curY += RecordRowH;
                    }

                    altBase = !altBase;
                }
            }

            GUI.EndScrollView();
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string MKey(TracedMethodInfo info) => $"{info.TypeName}.{info.MethodName}";

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
            if (_stylesInit) return;
            _stylesInit = true;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.88f, 0.88f, 0.96f) }
            };
            _styleSection = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            _styleRow = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorNormal }
            };
            _styleCps = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.65f, 0.92f, 0.65f) }
            };
            _styleMuted = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.48f, 0.50f, 0.54f) }
            };

            // ── Detail panel styles ──────────────────────────────────────
            _styleDetailTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.80f, 0.88f, 1.00f) }
            };
            _styleDetailColHdr = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.55f, 0.58f, 0.65f) }
            };
            _styleDetailAgo = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.50f, 0.53f, 0.60f) }
            };
            _styleDetailArg = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = ColorNormal }
            };
            _styleDetailResult = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.60f, 0.92f, 0.60f) }
            };
        }
    }
}
