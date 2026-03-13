using UnityEngine;

namespace ifp.arena.bep
{
    public partial class TracerOverlay
    {
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

            // Shadow + background
            DrawBox(new Rect(dx + 4, dy + 4, DetailWidth, dh), new Color(0, 0, 0, 0.30f));
            DrawBox(detailPanel, new Color(0.06f, 0.06f, 0.09f, 0.97f));

            bool   isHot     = info.CallsPerSecond >= FreqThreshold;
            int    showCount = isHot ? HotDisplayCount : ColdDisplayCount;
            string subtitle  = isHot
                ? $"last {HotDisplayCount} samples  (≈0.1s apart)"
                : $"last {ColdDisplayCount} calls";

            // Title bar
            Rect titleR = new Rect(dx, dy, DetailWidth, TitleHeight);
            DrawBox(titleR, new Color(0.10f, 0.16f, 0.28f, 1f));
            GUI.Label(Inset(titleR, 10, 0), $"📄  {info.MethodName}", _styleDetailTitle);
            GUI.Label(new Rect(titleR.xMax - 210, titleR.y, 202, TitleHeight), subtitle, _styleMuted);

            // Freeze hint bar
            float hintY = dy + TitleHeight;
            Rect  hintR = new Rect(dx, hintY, DetailWidth, RecordRowH);
            bool  frozen = _frozenSnapshot != null;
            DrawBox(hintR, frozen
                ? new Color(0.22f, 0.16f, 0.04f, 1f)
                : new Color(0.09f, 0.09f, 0.13f, 1f));
            GUI.Label(Inset(hintR, 8, 0),
                frozen
                    ? "⏸  FROZEN  —  collapse all records to resume live"
                    : "click a record to expand  ·  click again to collapse",
                _styleDetailColHdr);

            float bodyTop = hintY + RecordRowH;
            float bodyH   = detailPanel.yMax - bodyTop;

            var records = _frozenSnapshot ?? info.History.GetSnapshot(showCount);

            // Dynamic content height — expanded records have extra sub-rows
            float totalH = 0f;
            for (int i = 0; i < records.Length; i++)
            {
                bool exp      = _expandedRecords.Contains(records[i].Timestamp);
                int  argCount = records[i].Args?.Length ?? 0;
                totalH += exp ? RecordRowH * (2 + Mathf.Max(argCount, 1)) : RecordRowH;
            }
            totalH = Mathf.Max(totalH, bodyH);

            bool needsScroll = totalH > bodyH;
            Rect viewR    = new Rect(dx, bodyTop, DetailWidth, bodyH);
            Rect contentR = new Rect(0, 0, DetailWidth - (needsScroll ? 14f : 0f), totalH);
            float now     = Time.realtimeSinceStartup;

            _scrollDetail = GUI.BeginScrollView(viewR, _scrollDetail, contentR, false, needsScroll);

            if (records.Length == 0)
            {
                DrawBox(new Rect(0, 0, contentR.width, RecordRowH), new Color(0.09f, 0.09f, 0.11f, 0.85f));
                GUI.Label(Inset(new Rect(0, 0, contentR.width, RecordRowH), 14, 0),
                    "no history yet — waiting for calls…", _styleMuted);
            }
            else
            {
                float curY   = 0f;
                bool  altBase = false;

                for (int i = 0; i < records.Length; i++)
                {
                    var  rec      = records[i];
                    bool expanded = _expandedRecords.Contains(rec.Timestamp);
                    int  argCount = rec.Args?.Length ?? 0;

                    float age = now - rec.Timestamp;
                    float ft  = Mathf.Clamp01(age / FlashDuration);
                    Color valueCol = Color.Lerp(ColorFlash, ColorNormal, ft);

                    string agoStr = age < 1f ? $"{age * 1000f:0}ms"
                                  : age < 60f ? $"{age:0.0}s" : "old";

                    Color headerBg = altBase
                        ? new Color(0.09f, 0.09f, 0.13f, 0.92f)
                        : new Color(0.12f, 0.12f, 0.17f, 0.92f);

                    Rect headerRow = new Rect(0, curY, contentR.width, RecordRowH);
                    DrawBox(headerRow, headerBg);

                    // Expand / collapse on click
                    if (GUI.Button(headerRow, GUIContent.none, GUIStyle.none))
                    {
                        if (expanded)
                        {
                            _expandedRecords.Remove(rec.Timestamp);
                            if (_expandedRecords.Count == 0)
                                _frozenSnapshot = null;
                        }
                        else
                        {
                            if (_frozenSnapshot == null)
                                _frozenSnapshot = info.History.GetSnapshot(showCount);
                            _expandedRecords.Add(rec.Timestamp);
                        }
                    }

                    // Expand arrow
                    DrawColoredLabel(new Rect(headerRow.x + 6, headerRow.y, 16, RecordRowH),
                        expanded ? "▼" : "▶", _styleDetailAgo,
                        expanded ? ColorSelected : new Color(0.42f, 0.44f, 0.50f));

                    // Age label
                    GUI.Label(new Rect(headerRow.x + 24, headerRow.y, 52, RecordRowH), agoStr, _styleDetailAgo);

                    if (!expanded)
                    {
                        // Collapsed — single-line summary
                        const float resW = 100f;
                        float sumX = headerRow.x + 80f;
                        float sumW = contentR.width - 80f - resW - 4f;

                        string argSummary = argCount == 0 ? "(no args)" : string.Join(",  ", rec.Args);
                        DrawColoredLabel(new Rect(sumX, headerRow.y, sumW, RecordRowH), argSummary, _styleDetailArg, valueCol);
                        DrawColoredLabel(new Rect(contentR.width - resW, headerRow.y, resW, RecordRowH),
                            rec.Result ?? "(void)", _styleDetailResult, valueCol);
                    }

                    curY += RecordRowH;

                    if (expanded)
                    {
                        const float subIndent = 28f;
                        const float typeW     = 34f;
                        const float nameW     = 130f;
                        const float eqW       = 16f;

                        Color subBg = altBase
                            ? new Color(0.07f, 0.07f, 0.10f, 0.92f)
                            : new Color(0.09f, 0.09f, 0.13f, 0.92f);

                        // Argument sub-rows
                        if (argCount == 0)
                        {
                            DrawBox(new Rect(0, curY, contentR.width, RecordRowH), subBg);
                            DrawColoredLabel(new Rect(subIndent, curY, contentR.width - subIndent, RecordRowH),
                                "(no arguments)", _styleDetailAgo, new Color(0.42f, 0.44f, 0.50f));
                            curY += RecordRowH;
                        }
                        else
                        {
                            foreach (string argStr in rec.Args)
                            {
                                DrawBox(new Rect(0, curY, contentR.width, RecordRowH), subBg);

                                DrawColoredLabel(new Rect(subIndent, curY, typeW, RecordRowH),
                                    "arg", _styleDetailColHdr, new Color(0.40f, 0.43f, 0.52f));

                                int eqIdx = argStr.IndexOf('=');
                                if (eqIdx > 0)
                                {
                                    string pName = argStr.Substring(0, eqIdx);
                                    string pVal  = argStr.Substring(eqIdx + 1);

                                    GUI.Label(new Rect(subIndent + typeW, curY, nameW, RecordRowH), pName, _styleDetailAgo);
                                    DrawColoredLabel(new Rect(subIndent + typeW + nameW, curY, eqW, RecordRowH),
                                        "=", _styleDetailColHdr, new Color(0.38f, 0.40f, 0.46f));
                                    DrawColoredLabel(
                                        new Rect(subIndent + typeW + nameW + eqW, curY,
                                            contentR.width - subIndent - typeW - nameW - eqW, RecordRowH),
                                        pVal, _styleDetailArg, valueCol);
                                }
                                else
                                {
                                    DrawColoredLabel(
                                        new Rect(subIndent + typeW, curY, contentR.width - subIndent - typeW, RecordRowH),
                                        argStr, _styleDetailArg, valueCol);
                                }

                                curY += RecordRowH;
                            }
                        }

                        // Result sub-row
                        DrawBox(new Rect(0, curY, contentR.width, RecordRowH), subBg);
                        DrawColoredLabel(new Rect(subIndent, curY, typeW, RecordRowH),
                            "↩", _styleDetailColHdr, new Color(0.40f, 0.65f, 0.40f));
                        DrawColoredLabel(
                            new Rect(subIndent + typeW, curY, contentR.width - subIndent - typeW, RecordRowH),
                            rec.Result ?? "(void)", _styleDetailResult, valueCol);
                        curY += RecordRowH;
                    }

                    altBase = !altBase;
                }
            }

            GUI.EndScrollView();
        }
    }
}
