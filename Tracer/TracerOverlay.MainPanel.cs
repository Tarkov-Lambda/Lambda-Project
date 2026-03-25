using System.Collections.Generic;
using UnityEngine;

namespace ifp.tracer
{
    public partial class TracerOverlay
    {
        private void DrawMainPanel()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            float px = sw - PanelWidth - 16f;
            float py = PanelPadY;
            float ph = sh - PanelPadY * 2f;

            Rect panel = new Rect(px, py, PanelWidth, ph);

            // shadow + background
            DrawBox(new Rect(px + 4, py + 4, PanelWidth, ph), new Color(0, 0, 0, 0.30f));
            DrawBox(panel, new Color(0.07f, 0.07f, 0.09f, 0.97f));

            // title bar
            Rect titleR = new Rect(px, py, PanelWidth, TitleHeight);
            DrawBox(titleR, new Color(0.12f, 0.12f, 0.17f, 1f));

            string types = DynamicClassTracer.TracerLabels.Count == 0
                ? "no active tracers"
                : string.Join(", ", DynamicClassTracer.TracerLabels.Values);

            GUI.Label(Inset(titleR, 10, 0), $"🔍  TRACER  —  {types}", _styleTitle);
            GUI.Label(new Rect(titleR.xMax - 90, titleR.y, 82, TitleHeight), "F3 hide", _styleMuted);

            // ph minus: title bar + 4 px gap + 2 section headers + 1 section gap
            float bodyAvail = ph - TitleHeight - 4f - HeaderHeight * 2f - SectionGap;
            float hotBodyH  = bodyAvail * 0.50f;
            float cldBodyH  = bodyAvail * 0.50f;

            float cur = py + TitleHeight + 4f;

            cur = DrawMethodSection(
                px, cur, PanelWidth, hotBodyH,
                $"HIGH FREQUENCY  (> {FreqThreshold:0} / sec)",
                new Color(0.50f, 0.22f, 0.04f, 0.95f),
                _hotSnapshot, ref _scrollHot);

            cur += SectionGap;

            DrawMethodSection(
                px, cur, PanelWidth, cldBodyH,
                "MANUAL / EVENT",
                new Color(0.06f, 0.22f, 0.42f, 0.95f),
                _coldSnapshot, ref _scrollCold);
        }

        private float DrawMethodSection(
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

            bool  needsScroll = rows.Count * RowHeight > bodyH;
            Rect  viewR       = new Rect(x, top, w, bodyH);
            Rect  contentR    = new Rect(0, 0, w - (needsScroll ? 14f : 0f), rows.Count * RowHeight);
            float now         = Time.realtimeSinceStartup;

            scroll = GUI.BeginScrollView(viewR, scroll, contentR, false, needsScroll);

            for (int i = 0; i < rows.Count; i++)
            {
                var  info     = rows[i];
                bool alt      = i % 2 == 1;
                bool selected = _selectedKey == MKey(info);
                Rect row      = new Rect(0, i * RowHeight, contentR.width, RowHeight);

                Color bg = selected
                    ? new Color(0.14f, 0.22f, 0.40f, 0.98f)
                    : alt ? new Color(0.09f, 0.09f, 0.12f, 0.85f)
                           : new Color(0.12f, 0.12f, 0.15f, 0.85f);
                DrawBox(row, bg);

                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                    SelectMethod(info, selected);

                if (selected)
                {
                    Color prev = GUI.contentColor;
                    GUI.contentColor = ColorSelected;
                    GUI.Label(new Rect(row.x + 2, row.y, 12, RowHeight), "▶", _styleMuted);
                    GUI.contentColor = prev;
                }

                float t         = Mathf.Clamp01((now - info.LastCallTime) / FlashDuration);
                Color nameColor = selected ? ColorSelected : Color.Lerp(ColorFlash, ColorNormal, t);

                float nameW = row.width * 0.55f;
                DrawColoredLabel(Inset(new Rect(row.x + 14, row.y, nameW - 14, RowHeight), 2, 0),
                    info.MethodName, _styleRow, nameColor);

                float cpsX = row.x + nameW;
                float cpsW = row.width * 0.20f;
                string cpsStr = info.CallsPerSecond >= 1f ? $"{info.CallsPerSecond:0.0}/s"
                              : info.CallsPerSecond  > 0f ? "<1/s" : "0/s";
                GUI.Label(new Rect(cpsX, row.y, cpsW, RowHeight), cpsStr, _styleCps);

                GUI.Label(new Rect(cpsX + cpsW, row.y, row.width - nameW - cpsW, RowHeight),
                    $"total: {info.TotalCalls:N0}", _styleMuted);
            }

            GUI.EndScrollView();
            return top + bodyH;
        }

        private void DrawPropertiesPanel()
        {
            float sh = Screen.height;

            float px = 16f;
            float py = PanelPadY;
            float ph = sh - PanelPadY * 2f;

            Rect panel = new Rect(px, py, PropsPanelWidth, ph);

            // shadow + background
            DrawBox(new Rect(px + 4, py + 4, PropsPanelWidth, ph), new Color(0, 0, 0, 0.30f));
            DrawBox(panel, new Color(0.07f, 0.07f, 0.09f, 0.97f));

            // title bar
            Rect titleR = new Rect(px, py, PropsPanelWidth, TitleHeight);
            DrawBox(titleR, new Color(0.08f, 0.20f, 0.10f, 1f));
            GUI.Label(Inset(titleR, 10, 0), "📊  PROPERTIES  (live)", _styleTitle);
            GUI.Label(new Rect(titleR.xMax - 110, titleR.y, 102, TitleHeight),
                _propsSnapshot.Count == 0 ? "none" : $"{_propsSnapshot.Count} getters", _styleMuted);

            float top   = py + TitleHeight;
            float bodyH = ph - TitleHeight;

            if (_propsSnapshot.Count == 0)
            {
                Rect empty = new Rect(px, top, PropsPanelWidth, RowHeight);
                DrawBox(empty, new Color(0.09f, 0.09f, 0.11f, 0.85f));
                GUI.Label(Inset(empty, 14, 0), "—  no property getters traced yet", _styleMuted);
                return;
            }

            bool  needsScroll = _propsSnapshot.Count * RowHeight > bodyH;
            Rect  viewR       = new Rect(px, top, PropsPanelWidth, bodyH);
            Rect  contentR    = new Rect(0, 0, PropsPanelWidth - (needsScroll ? 14f : 0f), _propsSnapshot.Count * RowHeight);
            bool  multiType   = DynamicClassTracer.TracerLabels.Count > 1;
            float now         = Time.realtimeSinceStartup;

            // column widths
            const float indent = 14f;
            const float typeW  = 108f;
            const float arrowW = 22f;

            _scrollProps = GUI.BeginScrollView(viewR, _scrollProps, contentR, false, needsScroll);

            for (int i = 0; i < _propsSnapshot.Count; i++)
            {
                var  info     = _propsSnapshot[i];
                bool selected = _selectedKey == MKey(info);
                bool alt      = i % 2 == 1;
                Rect row      = new Rect(0, i * RowHeight, contentR.width, RowHeight);

                // background — green tint to distinguish from hot/cold
                Color bg = selected
                    ? new Color(0.10f, 0.24f, 0.16f, 0.98f)
                    : alt ? new Color(0.08f, 0.11f, 0.09f, 0.85f)
                           : new Color(0.10f, 0.14f, 0.11f, 0.85f);
                DrawBox(row, bg);

                // click to open detail panel (full call history)
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                    SelectMethod(info, selected);

                // selection arrow
                if (selected)
                    DrawColoredLabel(new Rect(row.x + 2, row.y, 12, RowHeight), "▶", _styleDetailAgo, ColorSelected);

                // flash red immediately after a call, fading to normal over FlashDuration
                float t = Mathf.Clamp01((now - info.LastCallTime) / FlashDuration);

                float curX = indent;

                // optional type name column (muted) when multiple tracers are active
                if (multiType)
                {
                    DrawColoredLabel(new Rect(curX, row.y, typeW, RowHeight),
                        info.TypeName, _styleDetailAgo, new Color(0.38f, 0.42f, 0.52f));
                    curX += typeW;
                }

                // property name — strip "get_" prefix
                string propName = info.MethodName.Length > 4
                    ? info.MethodName.Substring(4)
                    : info.MethodName;

                float remainingW = contentR.width - curX - arrowW;
                float nameW      = remainingW * 0.42f;
                float valueW     = remainingW - nameW;

                Color nameCol = selected ? ColorSelected : Color.Lerp(ColorFlash, ColorNormal, t);
                DrawColoredLabel(new Rect(curX, row.y, nameW, RowHeight), propName, _stylePropName, nameCol);
                curX += nameW;

                DrawColoredLabel(new Rect(curX, row.y, arrowW, RowHeight),
                    "→", _styleDetailColHdr, new Color(0.36f, 0.40f, 0.46f));
                curX += arrowW;

                // latest getter return value — flashes on change
                var    snap     = info.History.GetSnapshot(1);
                string valueStr = snap.Length > 0 ? (snap[0].Result ?? "(void)") : "—";
                Color  valCol   = Color.Lerp(ColorFlash, ColorPropVal, t);

                DrawColoredLabel(new Rect(curX, row.y, valueW, RowHeight), valueStr, _stylePropValue, valCol);
            }

            GUI.EndScrollView();
        }

        private void SelectMethod(TracedMethodInfo info, bool isCurrentlySelected)
        {
            string newKey = isCurrentlySelected ? null : MKey(info);
            if (newKey != _selectedKey)
            {
                _expandedRecords.Clear();
                _frozenSnapshot = null;
            }
            _selectedKey  = newKey;
            _scrollDetail = Vector2.zero;
        }
    }
}
